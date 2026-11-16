"""Train a bounded queue speed residual using anonymized video pace sequences."""
import argparse
from collections import defaultdict
import json
from pathlib import Path

import numpy as np

LIMITS = np.array([[-.28,.65],[-1.0,1.05],[0,1.30]])
SHAPE = (8,8,7)
ACTIONS = np.array([-.20,-.10,0.,.10,.20])
DT = .1
WIDTH = 96
TRAIN_CLIPS = ("000750332","000753404","000758618","000805406")
TEST_CLIPS = ("000810226","000902350")


def tracks(dataset, clips):
    result = []
    for name in clips:
        sequences = defaultdict(list)
        for row in dataset[name]["rows"]:
            if row["speed"] is not None and row["scale"] >= 18:
                sequences[row["track"]].append(row)
        for samples in sequences.values():
            runs, group = [], []
            for row in samples:
                if group and row["t"] - group[-1]["t"] > .25:
                    runs.append(group)
                    group = []
                group.append(row)
            runs.append(group)
            for run in runs:
                if len(run) < 7:
                    continue
                speed = np.linalg.norm([row["speed"] for row in run],axis=1)
                if np.quantile(speed,.8)<.20 or np.quantile(speed,.8)>2.8:
                    continue
                v = np.stack([np.roll(speed,-1),speed,np.roll(speed,1)])
                speed = np.median(v,axis=0)[1:-1]
                if speed.max()-speed.min()<.23:
                    continue
                # A monocular handheld image has no metric ground velocity.
                # Use only its timing and relative pace to drive the simulator.
                speed=np.clip(speed/max(.35,np.quantile(speed,.90)),0,1.22)
                result.append(np.interp(np.arange(0,len(speed)-1,.5),np.arange(len(speed)),speed))
    return result


def lead_runs(rng, patterns, n, scenario=False):
    t=np.linspace(0,9.5,WIDTH)[None,:]
    peak=rng.uniform(.68,1.32,(n,1))
    start=rng.uniform(.55,2.4,(n,1))
    ramp=rng.uniform(.28,1.2,(n,1))
    onset=np.clip((t-start)/ramp,0,1)
    stop=rng.uniform(3.3,7.0,(n,1))
    pause=rng.uniform(.36,1.4,(n,1))
    slow=np.clip((t-stop)/.30,0,1)*(1-np.clip((t-stop-pause)/.54,0,1))
    factor=rng.uniform(.43,1.0,(n,1))
    velocity=peak*onset*(1-factor*slow)
    if patterns:
        for i in range(n//2):
            sample=patterns[int(rng.integers(len(patterns)))]
            begin=int(rng.integers(14,42))
            count=min(WIDTH-begin-6,len(sample))
            if count<4:continue
            current=velocity[i,begin]
            segment=(.30*current+.70*sample[:count]*float(peak[i,0]))
            velocity[i,begin:begin+count]=segment
            ending=segment[-1]
            velocity[i,begin+count:begin+count+5]=np.linspace(ending,velocity[i,begin+count+4],5)
    # Fixed physics acceptance envelope also applies to replayed lead inputs.
    for i in range(1,WIDTH):
        velocity[:,i]=np.clip(velocity[:,i],velocity[:,i-1]-.34,velocity[:,i-1]+.23)
    return velocity


def states(error, relative, lead):
    v=np.stack((error,relative,lead),axis=-1)
    scale=(v-LIMITS[:,0])/(LIMITS[:,1]-LIMITS[:,0])*(np.asarray(SHAPE)-1)
    return np.clip(scale,0,np.asarray(SHAPE)-1)


def transitions(gap, speed, lead, next_lead, comfort, clearance, residual):
    goal=np.clip(lead+1.55*(gap-comfort)+residual,0,1.45)
    vmax=np.sqrt(lead*lead+7.6*np.maximum(0,gap-clearance))
    goal=np.minimum(goal,vmax)
    new_speed=np.clip(goal,speed-.38,speed+.23)
    change=new_speed-speed
    next_gap=gap+DT*(next_lead-new_speed)
    return next_gap,new_speed,change


def train(data, seed=20261116):
    rng=np.random.default_rng(seed)
    q=np.zeros((*SHAPE,len(ACTIONS)),np.float32)
    counts=np.zeros(SHAPE,np.int32)
    runs=160
    take=24
    for batch in range(runs):
        n=take
        profiles=lead_runs(rng,data,n)
        gap=rng.uniform(.57,.91,n)
        own=np.zeros(n)
        previous=np.zeros(n)
        body=rng.uniform(.45,.55,n)
        space=rng.uniform(.67,.83,n)
        explore=.58-.44*batch/runs
        for i in range(WIDTH-1):
            lead,after=profiles[:,i:i+2].T
            comfort=space+own*.08
            index=tuple(np.rint(states(gap-comfort,lead-own,lead)).astype(int).T)
            old=q[index]
            choices=np.where(rng.random(n)<explore,rng.integers(len(ACTIONS),size=n),old.argmax(axis=1))
            offsets=ACTIONS[choices]
            new_gap,new_speed,change=transitions(gap,own,lead,after,comfort,body,offsets)
            shock=change-previous
            error=new_gap-(space+new_speed*.08)
            reward=-5.2*error**2-2.5*np.maximum(0,body+.07-new_gap)*4-2.1*shock**2-.22*change**2
            reward-=.22*np.maximum(0,lead-new_speed)*np.clip((new_gap-space+.09)*5,0,1)
            reward-=.12*(offsets**2)
            new_index=tuple(np.rint(states(error,after-new_speed,after)).astype(int).T)
            ix=index+(choices,)
            delta=.095*(reward+.93*q[new_index].max(axis=1)-q[ix])
            np.add.at(q, ix, np.clip(delta,-.45,.42))
            np.add.at(counts,index,1)
            gap,own,previous=new_gap,new_speed,change
    best=q.argmax(axis=-1)
    values=ACTIONS[best]
    # Rarely visited states inherit the continuous deterministic controller.
    values[counts<28]=0
    return values,counts,int(runs*take*(WIDTH-1))


def interpolate(table, s):
    lower=s.astype(int)
    upper=np.minimum(lower+1,np.asarray(SHAPE)-1)
    frac=s-lower
    result=np.zeros(len(s))
    for (a,b,c) in np.ndindex(2,2,2):
        chosen=(a,b,c)
        pos=tuple(upper[:,i] if on else lower[:,i] for i,on in enumerate(chosen))
        coeff=np.prod(np.stack([frac[:,i] if on else 1-frac[:,i] for i,on in enumerate(chosen)],axis=1),axis=1)
        result+=table[pos]*coeff
    return result


def evaluate(patterns, policy):
    rng=np.random.default_rng(14712)
    n=54
    profiles=lead_runs(rng,patterns,n)
    gap=rng.uniform(.59,.91,n)
    own=np.zeros(n)
    previous=np.zeros(n)
    body=rng.uniform(.45,.55,n)
    space=rng.uniform(.67,.83,n)
    shocks=[]
    spaces=[]
    speeds=[]
    near=0
    stalled=0
    for i in range(WIDTH-1):
        lead,after=profiles[:,i:i+2].T
        comfort=space+own*.08
        delta=np.zeros(n) if policy is None else interpolate(policy,states(gap-comfort,lead-own,lead))
        gap,own,change=transitions(gap,own,lead,after,comfort,body,delta)
        shocks.append(np.abs(change-previous))
        spaces.append(np.abs(gap-comfort))
        speeds.append(own)
        near+=np.count_nonzero(gap<body+.025)
        stalled+=np.count_nonzero((lead>.25)&(own<.055))
        previous=change
    return dict(scenarios=n,contactFrames=int(near),stallFrames=int(stalled),
                jerkMean=float(np.mean(shocks)),jerkP95=float(np.quantile(shocks,.95)),
                meanSpeed=float(np.mean(speeds)),gapError=float(np.mean(spaces)))


def main():
    args=argparse.ArgumentParser(description=__doc__)
    args.add_argument("tracks",type=Path)
    args.add_argument("output",type=Path)
    opts=args.parse_args()
    data=json.loads(opts.tracks.read_text(encoding="utf-8"))
    examples=tracks(data,TRAIN_CLIPS)
    holdout=tracks(data,TEST_CLIPS)
    if not examples or not holdout:
        raise ValueError(f"insufficient accepted tracks {len(examples)} train {len(holdout)} holdout")
    policy,visits,n=train(examples)
    baseline=evaluate(holdout,None)
    # Keep residual authority small enough to preserve the existing safety layer.
    policy*=.65
    candidate=evaluate(holdout,policy)
    result={"trainVideoWindows":len(examples),"heldOutVideoWindows":len(holdout),"trainingTransitions":n,
            "baseline":baseline,"candidate":candidate}
    print(json.dumps(result,indent=2))
    opts.output.write_text(json.dumps({"version":1,"residual":[round(float(x),5) for x in policy.ravel()]},
                                      separators=(",",":"))+"\n",encoding="utf-8")
    opts.output.with_suffix(".evaluation.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")

if __name__=="__main__":
    main()

