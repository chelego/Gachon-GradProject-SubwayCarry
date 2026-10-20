"""Held-out Unity rollouts: learned ONNX, no assistance and random intent.
The Unity executable supplies observations, rewards and terminal states.
No Python copy of the training dynamics is used for evaluation.
"""
import argparse, json, sys, logging
from collections import defaultdict
from pathlib import Path
import numpy as np
import onnxruntime as ort
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel

sys.stdout.reconfigure(encoding='utf-8')
logging.getLogger('mlagents_envs').setLevel(logging.ERROR)
p=argparse.ArgumentParser()
p.add_argument('--env',required=True);p.add_argument('--model',required=True)
p.add_argument('--episodes',type=int,default=400);p.add_argument('--out',required=True)
args=p.parse_args()
session=ort.InferenceSession(args.model,providers=['CPUExecutionProvider'])
print('ONNX inputs',[(x.name,x.shape)for x in session.get_inputs()],flush=True)
print('ONNX outputs',[(x.name,x.shape)for x in session.get_outputs()],flush=True)

def case(observation):
    if observation[8]>.5:return 'balance'
    if observation[9]>.65:return 'fatigue'
    if observation[11]>.3:return 'blocked'
    return 'normal'

def rollout(mode,seed):
    channel=EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=30,quality_level=0,target_frame_rate=-1,capture_frame_rate=60)
    stats_channel=StatsSideChannel()
    env=UnityEnvironment(file_name=args.env,seed=seed,worker_id=10,base_port=5100,no_graphics=True,side_channels=[channel,stats_channel],timeout_wait=120)
    totals=defaultdict(lambda:defaultdict(float));active={};rng=np.random.default_rng(seed)
    try:
        env.reset();name=list(env.behavior_specs)[0]
        completed=0
        while completed<args.episodes:
            decision,terminal=env.get_steps(name)
            for i,agent_id in enumerate(terminal.agent_id):
                if completed>=args.episodes:break
                if agent_id not in active:continue
                record=active.pop(agent_id);group=record['case'];stats=totals[group]
                stats['episodes']+=1;stats['success']+=float(terminal.reward[i]>.7)
                stats['falls']+=float(terminal.reward[i]<-.7)
                stats['reward']+=record['reward']+float(terminal.reward[i]);stats['steps']+=record['steps']
                stats['help_steps']+=record['help'];completed+=1
                for action,count in enumerate(record['actions']):stats['action_'+str(action)]+=count
            n=len(decision)
            if n:
                obs=decision.obs[0].astype(np.float32)
                if mode=='none':actions=np.zeros(n,dtype=np.int32)
                elif mode=='random':actions=rng.integers(0,4,n,dtype=np.int32)
                else:
                    feed={}
                    for inp in session.get_inputs():
                        feed[inp.name]=np.ones((n,4),np.float32) if 'mask' in inp.name else obs
                    output=session.run(None,feed)
                    names=[x.name for x in session.get_outputs()]
                    target=next((i for i,x in enumerate(names)if 'deterministic_discrete' in x),None)
                    if target is None:target=next(i for i,x in enumerate(names)if x=='discrete_actions')
                    actions=output[target].reshape(n,-1)[:,0].astype(np.int32)
                for i,agent_id in enumerate(decision.agent_id):
                    if agent_id not in active:active[agent_id]={'case':case(obs[i]),'steps':0,'reward':0.,'help':0,'actions':[0,0,0,0]}
                    record=active[agent_id];record['reward']+=float(decision.reward[i]);record['steps']+=1;record['help']+=int(actions[i]!=0)
                    record['actions'][int(actions[i])]+=1
                env.set_actions(name,ActionTuple(discrete=actions.reshape(-1,1)))
            env.step()
            stats_channel.get_and_reset_stats()
        result={k:dict(v)for k,v in totals.items()}
        print(mode,json.dumps(result),flush=True);return result
    finally:env.close()

results={mode:rollout(mode,20261018)for mode in ['none','random','trained']}
Path(args.out).write_text(json.dumps({'seed':20261018,'episodes_requested_per_policy':args.episodes,'results':results},ensure_ascii=False,indent=2),encoding='utf-8')
