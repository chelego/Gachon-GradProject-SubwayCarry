"""PPO fine-tuning of SubwayCarry's bounded local steering prior.

Train on sequential synthetic encounters. Checkpoint selection uses validation
seeds. A separate test bank compares rule, imitation, and PPO with identical starts.
"""
import argparse
import json
import math
import time
from pathlib import Path

import torch
from torch import nn


DT = .15
STEPS = 48
GOAL = 4.7
TAGS = ('head_on', 'crossing', 'slow_flow', 'narrow')
SEED = 7419
VALIDATION_SEED = 61971
TEST_SEED = 217823


class Arena:
    def __init__(self, count, seed, device, holdout=False):
        self.device = device
        rng = torch.Generator(device=device).manual_seed(seed)

        def rand(*shape):
            return torch.rand(shape, generator=rng, device=device)

        self.kind = torch.arange(count, device=device) % len(TAGS)
        self.p = torch.zeros(count, 2, device=device)
        self.v = self.p.clone()
        self.speed = .86 + .75 * rand(count)
        self.width = torch.where(self.kind == 3, .72 + .22 * rand(count), 1.12 + .65 * rand(count))
        self.side = torch.zeros(count, device=device)
        self.side_until = torch.zeros_like(self.side)
        self.active = torch.ones(count, dtype=torch.bool, device=device)
        self.contact = torch.zeros_like(self.active)
        self.stop_time = torch.zeros_like(self.speed)
        self.reward = torch.zeros_like(self.speed)
        self.close_time = torch.zeros_like(self.speed)
        self.arrived = torch.zeros_like(self.active)
        self.arrival_step = torch.full_like(self.speed, STEPS)
        self.step_id = 0

        self.present = rand(count, 4) < (.44 if holdout else .38)
        self.present[:, 0] = True
        self.present[:, 1] |= self.kind == 3
        self.radius = .38 + .14 * rand(count, 4)
        self.others = torch.stack(((rand(count, 4) * 1.64 - .82) * self.width[:, None],
                                   1.25 + 3.4 * rand(count, 4)), -1)
        self.other_v = torch.stack((.22 * (rand(count, 4) - .5), 2.5 * rand(count, 4) - 1.25), -1)
        self.others[:, 0, 0] = .6 * (rand(count) - .5)
        self.others[:, 0, 1] = 1.9 + 1.75 * rand(count)
        self.other_v[:, 0, 1] = -.68 - .91 * rand(count)
        cross = self.kind == 1
        crossing_side = torch.where(rand(count) < .5, -1., 1.)
        self.others[cross, 0, 0] = (crossing_side * (.95 + .65 * rand(count)))[cross]
        self.other_v[cross, 0, 0] = (-crossing_side * (.76 + .68 * rand(count)))[cross]
        self.other_v[cross, 0, 1] = 0
        slow = self.kind == 2
        self.others[slow, 0, 1] -= .65
        self.other_v[slow, 0, 1] = (.24 + .48 * rand(count))[slow]
        narrow = self.kind == 3
        self.others[narrow, 1, 0] = (.64 * crossing_side)[narrow]
        self.other_v[narrow, 1] = 0
        self.nominal = self.other_v.clone()
        self.wave = rand(count, 4) * math.tau
        self.drift = (.16 if holdout else .11) * rand(count, 4)
        self.acceleration = 2.1 + 2.5 * rand(count)
        self.angles = torch.tensor([0, 25, 50, -25, -50], device=device).repeat_interleave(3)
        self.angles = torch.cat((self.angles, self.angles.new_zeros(1)))
        self.scales = torch.tensor([1, .65, .3] * 5 + [0], device=device)
        self.directions = torch.stack((self.angles.deg2rad().sin(), self.angles.deg2rad().cos()), -1)

    def observe(self):
        offset = self.others - self.p[:, None]
        mask = self.present & (offset[..., 1] >= -.5) & (offset[..., 1] <= 3.5) & (offset[..., 0].abs() <= 2)
        order = offset.square().sum(-1).masked_fill(~mask, 1e3).argsort(1)
        packed = torch.cat((offset, self.other_v, self.radius[..., None]), -1) * mask[..., None]
        packed = packed.gather(1, order[..., None].expand(-1, -1, 5))
        obs = torch.cat((self.speed[:, None], self.v, self.side[:, None], packed.flatten(1)), 1)

        preferred = self.speed[:, None].expand(-1, 16).clone()
        leader = self.present & (offset[..., 0].abs() < .43) & (offset[..., 1] > 0) & (offset[..., 1] < 3) & (self.other_v[..., 1] > .12)
        caps = (self.other_v[..., 1] + (offset[..., 1] - .72) / .7).clamp_min(0)
        preferred[:, :3] = torch.minimum(self.speed, caps.masked_fill(~leader, 10).amin(1))[:, None]
        candidate = preferred[..., None] * self.scales[None, :, None] * self.directions
        # Approximate the production rule filter in a planar corridor. Unity still
        # validates scene surfaces, reservations, and actual motor collisions.
        lateral = self.p[:, :1] + candidate[..., 0] * .45
        legal = lateral.abs() <= self.width[:, None]
        legal[:, -1] = True
        relative = (.3 * self.v[:, None, None] + .7 * candidate[:, :, None]) - self.other_v[:, None]
        ttc = (offset[:, None] * relative).sum(-1) / relative.square().sum(-1).clamp_min(.001)
        ttc = ttc.clamp(0, 1.5)
        gap = (offset[:, None] - relative * ttc[..., None]).norm(dim=-1) - self.radius[:, None]
        risk = torch.where(gap < 0, 42 - 100 * gap, (.15 - gap).clamp_min(0) * 8 * (1 - ttc / 2))
        risk = (risk * self.present[:, None]).sum(-1)
        rule = (1 - preferred * self.scales / self.speed[:, None]) * 1.15
        rule += .013 * self.angles.abs() + .08 * (self.angles < 0)
        rule += .32 * (candidate - self.v[:, None]).square().sum(-1)
        rule += 1.25 * (self.angles * self.side[:, None] < 0) + risk
        rule += .10 * (self.p[:, :1] + .7 * candidate[..., 0]).abs()
        safe = legal & (rule < 35)
        safe[:, -1] = True
        rule = rule.masked_fill(~safe, 1000)
        # Once all moving options are rejected, the motor waits.
        encounter_v = self.other_v - torch.stack((torch.zeros_like(self.speed), self.speed), -1)[:, None]
        close_t = (-(offset * encounter_v).sum(-1) / encounter_v.square().sum(-1).clamp_min(.01)).clamp(0, 1.45)
        encounter = (((offset + encounter_v * close_t[..., None]).square().sum(-1) < 1.05**2) & mask).any(1)
        return obs, rule, candidate, encounter

    def step(self, action, candidates):
        command = candidates.gather(1, action[:, None, None].expand(-1, 1, 2)).squeeze(1)
        change = command - self.v
        move = change * (self.acceleration[:, None] * DT / change.norm(dim=1, keepdim=True).clamp_min(.001)).clamp_max(1)
        next_v = self.v + move
        next_pos = self.p + next_v * DT
        next_pos[:, 0] = next_pos[:, 0].clamp(-self.width, self.width)
        self.other_v = self.nominal.clone()
        self.other_v[..., 0] += self.drift * torch.sin(self.wave + self.step_id * .29)
        self.other_v[..., 1] += .6 * self.drift * torch.sin(self.wave + self.step_id * .17)
        self.others += self.other_v * DT
        self.others[..., 0] = self.others[..., 0].clamp(-self.width[:, None], self.width[:, None])
        gap = ((self.others - next_pos[:, None]).norm(dim=-1) - self.radius).masked_fill(~self.present, 3).amin(1)
        touched = (gap < -.015) & self.active
        self.contact |= touched
        self.close_time += ((gap < .16) & self.active) * DT
        self.stop_time += ((next_v[:, 1] < .12) & self.active) * DT
        advance = next_pos[:, 1] - self.p[:, 1]
        reached = (next_pos[:, 1] >= GOAL) & self.active
        reward = (advance * 1.65 - .035 - (.22 - gap).clamp_min(0).square() * 6 - touched * 1.5
                  - .028 * move.square().sum(-1) - .02 * next_pos[:, 0].abs() + reached * 1.2)
        reward *= self.active
        self.reward += reward
        self.p = torch.where(self.active[:, None], next_pos, self.p)
        self.v = torch.where(self.active[:, None], next_v, torch.zeros_like(next_v))
        self.arrival_step = torch.where(reached, self.step_id + 1, self.arrival_step)
        self.arrived |= reached
        self.active &= ~reached
        committed = (self.angles[action].abs() > 0) & (command.square().sum(1) > .02)
        self.side_until = torch.where(committed, self.step_id + math.ceil(.85 / DT), self.side_until)
        self.side = torch.where(committed, self.angles[action].sign(), self.side * (self.step_id < self.side_until))
        self.step_id += 1
        return reward, self.active.clone()


def actor_net(device, source):
    model = nn.Sequential(nn.Linear(24, 40), nn.ReLU(), nn.Linear(40, 16)).to(device)
    with torch.no_grad():
        for layer, prefix in ((model[0], 'input'), (model[2], 'output')):
            layer.weight.copy_(torch.tensor(source[prefix + 'Weights'], device=device).reshape_as(layer.weight))
            layer.bias.copy_(torch.tensor(source[prefix + 'Bias'], device=device))
    return model


def combined(model, obs, rule, encounter):
    if model is None:
        return -rule
    scores = model(obs)
    penalty = ((scores.amax(dim=1, keepdim=True) - scores) * .18).clamp_max(.36)
    return -(rule + penalty * encounter[:, None])


@torch.no_grad()
def evaluate(model, count, seed, device, replay=False):
    arena = Arena(count, seed, device, holdout=True)
    trace = []
    shown = min(4, count)
    if replay:
        trace.append(torch.cat((arena.p[:shown, None], arena.others[:shown]), 1).flatten(1).cpu().tolist())
    for _ in range(STEPS):
        obs, rule, candidates, encounter = arena.observe()
        action = combined(model, obs, rule, encounter).argmax(1)
        arena.step(action, candidates)
        if replay:
            trace.append(torch.cat((arena.p[:shown, None], arena.others[:shown]), 1).flatten(1).cpu().tolist())

    def metrics(mask):
        return dict(episodes=int(mask.sum()), success=(arena.arrived & ~arena.contact)[mask].float().mean().item(),
                    contact=arena.contact[mask].float().mean().item(),
                    progress=arena.p[mask, 1].clamp_max(GOAL).mean().item(),
                    wait=arena.stop_time[mask].mean().item(), close=arena.close_time[mask].mean().item(),
                    reward=arena.reward[mask].mean().item())

    report = metrics(torch.ones_like(arena.active))
    report['cases'] = [metrics(arena.kind == i) for i in range(len(TAGS))]
    if replay:
        report['replay'] = [dict(width=arena.width[i].item(), present=arena.present[i].cpu().tolist(),
                                 contact=bool(arena.contact[i]), success=bool(arena.arrived[i] and not arena.contact[i]),
                                 points=[round(p, 4) for frame in trace for p in frame[i]]) for i in range(shown)]
    return report


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument('--base', type=Path, required=True)
    cli.add_argument('--output', type=Path, required=True)
    cli.add_argument('--updates', type=int, default=256)
    cli.add_argument('--arenas', type=int, default=1024)
    cli.add_argument('--progress', type=Path)
    args = cli.parse_args()
    torch.manual_seed(SEED)
    device = torch.device('cuda')
    started = time.perf_counter()
    source = json.loads(args.base.read_text(encoding='utf-8'))
    base = actor_net(device, source).eval().requires_grad_(False)
    actor = actor_net(device, source)
    critic = nn.Sequential(nn.Linear(24, 64), nn.Tanh(), nn.Linear(64, 1)).to(device)
    optimizer = torch.optim.AdamW(list(actor.parameters()) + list(critic.parameters()), lr=.00035)
    validation = evaluate(base, 2048, VALIDATION_SEED, device)
    best_value = float('-inf')
    selected = None
    history = [dict(updates=0, transitions=0, **{k:validation[k] for k in ('success','contact','reward','wait')})]
    transitions = 0

    def publish(update, complete=False):
        if args.progress is None:
            return
        temporary = args.progress.with_suffix('.tmp')
        snapshot = dict(updates=update, totalUpdates=args.updates, transitions=transitions,
                        seconds=time.perf_counter()-started, complete=complete, history=history)
        temporary.write_text(json.dumps(snapshot, separators=(',', ':')), encoding='utf-8')
        temporary.replace(args.progress)

    publish(0)
    print(f'GPU {torch.cuda.get_device_name()} | PyTorch {torch.__version__} | {args.arenas} parallel environments', flush=True)
    for update in range(1, args.updates + 1):
        arena = Arena(args.arenas, SEED + update * 101, device)
        states, rules, encounters, actions, old_logp, values, rewards, alive, valid = ([] for _ in range(9))
        with torch.no_grad():
            for _ in range(STEPS):
                obs, rule, candidates, encounter = arena.observe()
                distribution = torch.distributions.Categorical(logits=combined(actor, obs, rule, encounter) / .20, validate_args=False)
                action = distribution.sample()
                states.append(obs); rules.append(rule); encounters.append(encounter); actions.append(action)
                old_logp.append(distribution.log_prob(action)); values.append(critic(obs).squeeze(1)); valid.append(arena.active.clone())
                reward, next_active = arena.step(action, candidates)
                rewards.append(reward); alive.append(next_active)
            advantage = torch.zeros_like(values[0])
            advantages = []
            for i in reversed(range(STEPS)):
                next_value = values[i + 1] if i + 1 < STEPS else torch.zeros_like(advantage)
                delta = rewards[i] + .985 * next_value * alive[i] - values[i]
                advantage = delta + .985 * .94 * advantage * alive[i]
                advantages.append(advantage)
            mask = torch.cat(valid)
            adv = torch.cat(advantages[::-1])[mask]
            returns = torch.cat(values)[mask] + adv
            adv = (adv - adv.mean()) / adv.std().clamp_min(.001)
            x, c, e, a, old = [torch.cat(items)[mask] for items in (states, rules, encounters, actions, old_logp)]
            transitions += x.shape[0]

        for _ in range(3):
            for batch in torch.randperm(len(x), device=device).split(2048):
                logits = combined(actor, x[batch], c[batch], e[batch]) / .20
                distribution = torch.distributions.Categorical(logits=logits, validate_args=False)
                ratio = (distribution.log_prob(a[batch]) - old[batch]).exp()
                gain = torch.minimum(ratio * adv[batch], ratio.clamp(.8, 1.2) * adv[batch]).mean()
                value_loss = nn.functional.mse_loss(critic(x[batch]).squeeze(1), returns[batch])
                # Small anchor preserves the existing human-track prior on unseen inputs.
                anchor = nn.functional.smooth_l1_loss(actor(x[batch]), base(x[batch]))
                loss = -gain + .35 * value_loss - .008 * distribution.entropy().mean() + .018 * anchor
                optimizer.zero_grad(set_to_none=True)
                loss.backward()
                nn.utils.clip_grad_norm_(list(actor.parameters()) + list(critic.parameters()), .7)
                optimizer.step()

        if update == 1 or update % 8 == 0 or update == args.updates:
            metrics = evaluate(actor, 2048, VALIDATION_SEED, device)
            row = dict(updates=update, transitions=transitions, **{k:metrics[k] for k in ('success','contact','reward','wait')})
            history.append(row)
            score = metrics['reward'] + 4 * metrics['success'] - 5 * metrics['contact']
            if score > best_value:
                best_value = score
                selected = (update, transitions, {k:v.detach().clone() for k,v in actor.state_dict().items()})
            print('VALIDATION',json.dumps(row),flush=True)
            publish(update)

    update, chosen_transitions, weights = selected
    actor.load_state_dict(weights)
    comparison = {name:evaluate(model, 4096, TEST_SEED, device, replay=True)
                  for name,model in (('rule',None),('imitation',base),('ppo',actor))}
    report = dict(algorithm='PPO', device=torch.cuda.get_device_name(), seed=SEED, testSeed=TEST_SEED,
                  validationSeed=VALIDATION_SEED, arenas=args.arenas, transitions=transitions,
                  selectedTransitions=chosen_transitions, selectedUpdate=update, dt=DT, frames=STEPS+1,
                  goal=GOAL, seconds=round(time.perf_counter()-started,2), history=history, **comparison)
    args.output.mkdir(parents=True,exist_ok=True)
    output = dict(source, algorithm='PPO', ppoTransitions=chosen_transitions)
    for layer,prefix in ((actor[0],'input'),(actor[2],'output')):
        output[prefix+'Weights'] = layer.weight.detach().cpu().flatten().tolist()
        output[prefix+'Bias'] = layer.bias.detach().cpu().tolist()
    (args.output/'PassengerSteering.json').write_text(json.dumps(output,separators=(',',':'))+'\n',encoding='utf-8')
    (args.output/'PassengerLearningReport.json').write_text(json.dumps(report,separators=(',',':'))+'\n',encoding='utf-8')
    publish(args.updates, complete=True)
    print('TEST',json.dumps({k:{m:v for m,v in val.items() if m not in ('cases','replay')} for k,val in comparison.items()}),flush=True)
    print(f'Selected update {update}, transitions {chosen_transitions}; total {transitions}; {report["seconds"]:.1f}s',flush=True)


if __name__=='__main__':
    main()
