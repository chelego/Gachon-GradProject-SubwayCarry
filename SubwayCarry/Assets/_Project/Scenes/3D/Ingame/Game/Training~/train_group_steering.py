"""Shared-policy PPO for mutually reactive pedestrian groups.

Every active pedestrian observes and chooses a velocity at the same simulation
step. The critic also sees group state. Unity exports only the local actor.
"""
import argparse
import json
import math
from pathlib import Path
import time

import torch
from torch import nn

from train_passenger_ppo import actor_net


PEOPLE = 6
DT = .15
STEPS = 60
GOAL = 5.6
VALID = 491771
TEST = 764093


class Group:
    def __init__(self, count, seed, device, holdout=False):
        gen = torch.Generator(device=device).manual_seed(seed)
        rand = lambda *shape: torch.rand(shape, device=device, generator=gen)
        self.count = count
        self.kind = torch.arange(count, device=device) % 4
        self.phase = 0
        i = torch.arange(PEOPLE, device=device)[None, :].expand(count, -1)
        side = torch.where(i < 3, 1., -1.)
        two_way = (self.kind == 0) | (self.kind == 3)
        self.sign = torch.where(two_way[:, None], side, 1.)
        merge = self.kind == 1
        queue = self.kind == 2
        start = torch.where(two_way[:, None], i % 3, i)
        start = torch.where(merge[:, None], i % 3, start)
        x = .32 * (rand(count, PEOPLE) - .5)
        x += torch.where(merge[:, None], .90 * side, 0)
        x += torch.where(two_way[:, None], -.14 * side, 0)
        z = torch.where(self.sign > 0, -.86 * start, GOAL + .86 * start)
        self.p = torch.stack((x, z), -1)
        self.v = torch.zeros_like(self.p)
        self.speed = .94 + .68 * rand(count, PEOPLE)
        self.speed = torch.where(queue[:, None] & (i < 2), .42 + .27 * rand(count, PEOPLE), self.speed)
        self.radius = .20 + .052 * rand(count, PEOPLE)
        self.width = 1.16 + (.61 if holdout else .46) * rand(count, 1)
        self.gap = .64 + .22 * rand(count, 1)
        self.accel = 2.2 + 1.8 * rand(count, PEOPLE)
        self.side = torch.zeros_like(self.speed)
        self.side_until = torch.zeros_like(self.speed)
        self.done = torch.zeros_like(self.speed, dtype=torch.bool)
        self.touched = self.done.clone()
        self.wait = torch.zeros_like(self.speed)
        self.returned = torch.zeros_like(self.speed)
        self.flip = torch.zeros_like(self.speed)
        self.pair_steps = torch.zeros(count, device=device)
        self.angles = torch.tensor([0,25,50,-25,-50], device=device).repeat_interleave(3)
        self.angles = torch.cat((self.angles, self.angles.new_zeros(1)))
        self.scales = torch.tensor([1,.65,.3]*5+[0], device=device)
        self.directions = torch.stack((self.angles.deg2rad().sin(), self.angles.deg2rad().cos()), -1)
        self.self_mask = torch.eye(PEOPLE, device=device, dtype=torch.bool)[None, :, :]

    def limit(self, z):
        extra = (z - GOAL * .52).abs()
        shape = [self.count] + [1] * (z.ndim - 1)
        width = self.width.reshape(shape)
        neck = self.gap.reshape(shape)
        bottleneck = (self.kind == 3).reshape(shape)
        merge = (self.kind == 1).reshape(shape)
        merge_width = .76 + (3.45 - z).clamp_min(0) * .48
        corridor = torch.where(merge, torch.minimum(width, merge_width), width)
        return torch.where(bottleneck, torch.minimum(corridor, neck + (extra - .37).clamp_min(0) * .74), corridor)

    def observe(self):
        active = ~self.done
        # Rotate both local axes by 180 degrees for the opposite traffic flow.
        sign = self.sign[..., None]
        offset = (self.p[:, None] - self.p[:, :, None]) * sign[..., None]
        velocity = self.v[:, None] * sign[..., None]
        own = self.v * sign
        radius = self.radius[:, :, None] + self.radius[:, None]
        neighbors = active[:, None] & ~self.self_mask
        visible = neighbors & (offset[..., 0].abs() <= 2) & (offset[..., 1] >= -.5) & (offset[..., 1] <= 3.5)
        ids = offset.square().sum(-1).masked_fill(~visible, 1e3).argsort(-1)[..., :4]
        pack = torch.cat((offset, velocity, radius[..., None]), -1) * visible[..., None]
        pack = pack.gather(2, ids[..., None].expand(-1, -1, -1, 5)).flatten(2)
        obs = torch.cat((self.speed[..., None], own, self.side[..., None], pack), -1)

        preferred = self.speed[..., None].expand(-1, -1, 16).clone()
        leader = visible & (offset[..., 0].abs() < .43) & (offset[..., 1] > 0) & (offset[..., 1] < 3) & (velocity[..., 1] > .12)
        cap = (velocity[..., 1] + (offset[..., 1] - radius - .24) / .7).clamp_min(0)
        preferred[..., :3] = torch.minimum(self.speed, cap.masked_fill(~leader, 9).amin(-1))[..., None]
        candidate = preferred[..., None] * self.scales[:, None] * self.directions
        future = self.p[:, :, None] + .45 * candidate * sign[..., None]
        wall = future[..., 0].abs() <= self.limit(future[..., 1])
        relative = (.3 * own[:, :, None] + .7 * candidate)[..., None, :] - velocity[:, :, None]
        ttc = (offset[:, :, None] * relative).sum(-1) / relative.square().sum(-1).clamp_min(.001)
        ttc = ttc.clamp(0, 1.5)
        gap = (offset[:, :, None] - relative * ttc[..., None]).norm(dim=-1) - radius[:, :, None]
        separation = (offset.square().sum(-1) < radius.square())[:, :, None] & ((offset[:, :, None] * relative).sum(-1) < -.015)
        risk = torch.where(gap < 0, 42 - 100 * gap, (.15 - gap).clamp_min(0) * 8 * (1 - ttc / 2))
        risk = (risk * neighbors[:, :, None] * ~separation).sum(-1)
        rule = (1 - preferred * self.scales / self.speed[..., None]) * 1.15
        rule += .013 * self.angles.abs() + .08 * (self.angles < 0)
        rule += .32 * (candidate - own[:, :, None]).square().sum(-1)
        rule += 1.25 * (self.angles * self.side[..., None] < 0) + risk
        rule += .06 * future[..., 0].abs()
        allowed = wall & (rule < 35)
        allowed[..., -1] = True
        rule = rule.masked_fill(~allowed, 1000)
        direct = torch.stack((torch.zeros_like(self.speed), self.speed), -1)
        rel = velocity - direct[:, :, None]
        future_t = (-(offset * rel).sum(-1) / rel.square().sum(-1).clamp_min(.01)).clamp(0, 1.45)
        clearance = (offset + rel * future_t[..., None]).norm(dim=-1).masked_fill(~visible, 3).amin(-1)
        blend = ((1.12 - clearance) / .54).clamp(0, 1)
        direction_flow = (self.v[..., 1] * self.sign).clamp_min(0)
        group = torch.stack((active.float().mean(-1), direction_flow.mean(-1), self.touched.float().mean(-1),
                             ((direction_flow < .12) & active).float().mean(-1)), -1)
        critic = torch.cat((obs, group[:, None].expand(-1, PEOPLE, -1)), -1)
        return obs, critic, rule, candidate * sign[..., None], blend

    def step(self, action, candidates):
        active = ~self.done
        command = candidates.gather(2, action[..., None, None].expand(-1,-1,1,2)).squeeze(2)
        delta = command - self.v
        change = delta * (self.accel[..., None] * DT / delta.norm(dim=-1, keepdim=True).clamp_min(.001)).clamp_max(1)
        v = self.v + change
        p = self.p + v * DT
        width = self.limit(p[..., 1])
        p[..., 0] = p[..., 0].clamp(-width, width)
        gap = (p[:, None] - p[:, :, None]).norm(dim=-1) - self.radius[:, :, None] - self.radius[:, None]
        pair = active[:, None] & active[:, :, None] & ~self.self_mask
        contacts = (gap < -.015) & pair
        self.pair_steps += contacts.sum(dim=(1,2)) * .5
        contact = contacts.any(-1)
        self.touched |= contact
        nearest = gap.masked_fill(~pair, 3).amin(-1)
        progress = (p[..., 1] - self.p[..., 1]) * self.sign
        arrived = active & torch.where(self.sign > 0, p[..., 1] >= GOAL, p[..., 1] <= 0)
        turn = self.angles[action].sign()
        committed = (turn != 0) & (command.square().sum(-1) > .02)
        flip = committed & (turn * self.side < 0) & active
        self.flip += flip
        stop = (v[..., 1] * self.sign < .12) & active
        self.wait += stop * DT
        own_reward = (1.6 * progress + 1.25 * arrived - .034 - 2.6 * contact - .065 * stop - .12 * flip
                      - 9 * (.20 - nearest).clamp_min(0).square() - .04 * change.square().sum(-1)) * active
        reward = (own_reward + .25 * own_reward.sum(-1, keepdim=True) / active.sum(-1, keepdim=True).clamp_min(1)) * active
        self.returned += reward
        self.p = torch.where(active[..., None], p, self.p)
        self.done |= arrived
        self.v = torch.where(self.done[..., None], torch.zeros_like(v), v)
        self.side_until = torch.where(committed, self.phase + math.ceil(.85 / DT), self.side_until)
        self.side = torch.where(committed, turn, self.side * (self.phase < self.side_until))
        self.phase += 1
        return reward, ~self.done


def logits(actor, obs, rule, blend):
    if actor is None:return -rule
    scores = actor(obs)
    return -(rule + ((scores.amax(-1,keepdim=True) - scores)*.18).clamp_max(.36)*blend[...,None])


@torch.no_grad()
def evaluation(actor, count, seed, device, replay=False, old=False):
    arena = Group(count,seed,device,holdout=True)
    frames = [arena.p[:4].cpu().tolist()] if replay else None
    for _ in range(STEPS):
        obs,_,rule,candidates,blend=arena.observe()
        action=logits(actor,obs,rule,(blend>.13).float() if old else blend).argmax(-1)
        arena.step(action,candidates)
        if replay:frames.append(arena.p[:4].cpu().tolist())

    def row(mask):
        clean=arena.done[mask]&~arena.touched[mask]
        return dict(episodes=int(mask.sum()), pedestrians=int(mask.sum())*PEOPLE,success=clean.float().mean().item(),
            groupSuccess=clean.all(-1).float().mean().item(),contact=arena.touched[mask].float().mean().item(),
            wait=arena.wait[mask].mean().item(),reward=arena.returned[mask].mean().item(),
            throughput=arena.done[mask].sum(-1).float().mean().item()/(STEPS*DT),
            contactPairs=arena.pair_steps[mask].mean().item(), switches=arena.flip[mask].mean().item())
    result=row(torch.ones(count,dtype=torch.bool,device=device))
    result['cases']=[row(arena.kind==i) for i in range(4)]
    if replay:
        result['replay']=[dict(width=arena.width[i,0].item(),neck=arena.gap[i,0].item() if i==3 else 0,
            present=[True]*(PEOPLE-1),success=bool((arena.done[i]&~arena.touched[i]).all()),contact=bool(arena.touched[i].any()),
            points=[round(p,4) for f in frames for agent in f[i] for p in agent]) for i in range(4)]
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--updates',type=int,default=224)
    parser.add_argument('--arenas',type=int,default=384)
    parser.add_argument('--progress',type=Path)
    args=parser.parse_args()
    device=torch.device('cuda');torch.manual_seed(71592)
    started=time.perf_counter()
    source=json.loads(args.base.read_text(encoding='utf-8'))
    old=actor_net(device,source).eval().requires_grad_(False)
    actor=actor_net(device,source)
    critic=nn.Sequential(nn.Linear(28,96),nn.Tanh(),nn.Linear(96,64),nn.Tanh(),nn.Linear(64,1)).to(device)
    parameters=list(actor.parameters())+list(critic.parameters())
    optimizer=torch.optim.AdamW(parameters,lr=.00032)
    baseline=evaluation(old,1024,VALID,device,old=True)
    history=[dict(updates=0,transitions=0,**{k:baseline[k] for k in ('success','contact','reward','wait','groupSuccess')})]
    transitions=0
    best=None
    best_score=float('-inf')
    args.output.mkdir(parents=True,exist_ok=True)

    def publish(step,complete=False):
        if not args.progress:return
        args.progress.parent.mkdir(parents=True,exist_ok=True)
        pending=args.progress.with_suffix('.tmp')
        pending.write_text(json.dumps(dict(updates=step,totalUpdates=args.updates,transitions=transitions,
            seconds=time.perf_counter()-started,complete=complete,history=history),separators=(',',':')),encoding='utf-8')
        pending.replace(args.progress)

    publish(0)
    print(f'{torch.cuda.get_device_name()} | {args.arenas} parallel groups | {PEOPLE} reacting actors per group',flush=True)
    for step in range(1,args.updates+1):
        arena=Group(args.arenas,51961+step*79,device)
        rows=[]
        with torch.no_grad():
            for _ in range(STEPS):
                obs,global_obs,rule,candidates,blend=arena.observe()
                dist=torch.distributions.Categorical(logits=logits(actor,obs,rule,blend)/.18,validate_args=False)
                action=dist.sample()
                valid=~arena.done
                reward,alive=arena.step(action,candidates)
                rows.append((obs,global_obs,rule,blend,action,dist.log_prob(action),critic(global_obs).squeeze(-1),reward,alive,valid))
            advantage=torch.zeros_like(rows[0][6]); advantages=[]
            for i in range(STEPS-1,-1,-1):
                value=rows[i][6];next_value=rows[i+1][6] if i+1<STEPS else torch.zeros_like(value)
                alive=rows[i][8];advantage=rows[i][7]+.985*alive*(next_value+.94*advantage)-value
                advantages.append(advantage)
            valid=torch.cat([row[9] for row in rows])
            data=[torch.cat(column)[valid] for column in list(zip(*rows))[:7]]
            advantage=torch.cat(advantages[::-1])[valid]
            returns=data[6]+advantage
            advantage=(advantage-advantage.mean())/advantage.std().clamp_min(.001)
            transitions+=len(advantage)
        x,g,c,b,a,prior,_=data
        for _ in range(3):
            for idx in torch.randperm(len(x),device=device).split(4096):
                d=torch.distributions.Categorical(logits=logits(actor,x[idx],c[idx],b[idx])/.18,validate_args=False)
                ratio=(d.log_prob(a[idx])-prior[idx]).exp()
                gain=torch.minimum(ratio*advantage[idx],ratio.clamp(.8,1.2)*advantage[idx]).mean()
                loss=-gain+.35*nn.functional.mse_loss(critic(g[idx]).squeeze(-1),returns[idx])-.007*d.entropy().mean()
                loss+=.012*nn.functional.smooth_l1_loss(actor(x[idx]),old(x[idx]))
                optimizer.zero_grad(set_to_none=True);loss.backward()
                nn.utils.clip_grad_norm_(parameters,.7);optimizer.step()
        if step==1 or step%8==0 or step==args.updates:
            metrics=evaluation(actor,1024,VALID,device)
            row=dict(updates=step,transitions=transitions,**{k:metrics[k] for k in ('success','contact','reward','wait','groupSuccess')})
            history.append(row)
            score=4*metrics['groupSuccess']+4*metrics['success']-22*metrics['contact']-.27*metrics['wait']-.35*metrics['contactPairs']
            if score>best_score:
                best_score=score;best=(step,transitions,{k:v.detach().clone() for k,v in actor.state_dict().items()})
            print('VALIDATION',json.dumps(row),flush=True);publish(step)
    selected,learned,weights=best;actor.load_state_dict(weights)
    result={name:evaluation(model,4096,TEST,device,True,old=name=='imitation') for name,model in (('rule',None),('imitation',old),('ppo',actor))}
    report=dict(algorithm='GroupPPO',device=torch.cuda.get_device_name(),arenas=args.arenas,agentsPerArena=PEOPLE,
        transitions=transitions,selectedTransitions=learned,selectedUpdate=selected,frames=STEPS+1,dt=DT,goal=GOAL,
        testSeed=TEST,seconds=round(time.perf_counter()-started,2),history=history,**result)
    weights=dict(source,algorithm='GroupPPO',ppoTransitions=learned)
    for layer,prefix in ((actor[0],'input'),(actor[2],'output')):
        weights[prefix+'Weights']=layer.weight.detach().cpu().flatten().tolist()
        weights[prefix+'Bias']=layer.bias.detach().cpu().tolist()
    (args.output/'PassengerSteeringGroup.json').write_text(json.dumps(weights,separators=(',',':'))+'\n',encoding='utf-8')
    (args.output/'PassengerLearningReport.json').write_text(json.dumps(report,separators=(',',':'))+'\n',encoding='utf-8')
    print('TEST',json.dumps({name:{k:v for k,v in row.items() if k not in ('replay','cases')} for name,row in result.items()}),flush=True)
    publish(args.updates,True)


if __name__=='__main__':
    main()
