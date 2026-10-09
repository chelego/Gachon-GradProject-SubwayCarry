"""Small, dependency-free Acclaim ASF/AMC reader for the retained CMU recordings.
Source data and its usage terms are in MotionCapture/. No network at build time.
"""
import math, os
from mathutils import Vector, Matrix, Euler

WORLD=Matrix(((1,0,0),(0,0,-1),(0,1,0)))

def rotation(degrees):
    return Euler(tuple(math.radians(v) for v in degrees),'XYZ').to_matrix()

class Recording:
    def __init__(self,folder,take,fps):
        subject=take.split('_')[0];self.fps=fps;self.bones={};self.children={};self.frames=[]
        lines=[line.strip() for line in open(os.path.join(folder,subject+'.asf'),encoding='utf-8')]
        section='';bone=None;unit=.45
        for line in lines:
            if not line or line.startswith('#'): continue
            if line.startswith(':'): section=line;continue
            row=line.split()
            if section==':units' and row[0]=='length': unit=float(row[1])
            elif section==':bonedata':
                if line=='begin': bone={'dof':[]}
                elif line=='end': self.bones[bone['name']]=bone;bone=None
                elif bone is not None:
                    key=row[0]
                    if key=='name': bone['name']=row[1]
                    elif key=='direction': bone['direction']=Vector(tuple(map(float,row[1:4])))
                    elif key=='length': bone['length']=float(row[1])
                    elif key=='axis': bone['axis']=rotation(tuple(map(float,row[1:4])))
                    elif key=='dof': bone['dof']=row[1:]
            elif section==':hierarchy' and line not in ('begin','end'): self.children[row[0]]=row[1:]
        self.scale=.0254/unit
        frame=None
        for line in open(os.path.join(folder,take+'.amc'),encoding='utf-8'):
            row=line.split()
            if not row or row[0].startswith(('#',':')): continue
            if row[0].isdigit():
                frame={};self.frames.append(frame)
            else: frame[row[0]]=tuple(map(float,row[1:]))
        if not self.frames: raise ValueError('Empty CMU recording '+take)
        self.take=take
        self.cached=[self._evaluate(frame) for frame in self.frames]

    def _evaluate(self,frame):
        root=frame['root'];positions={'root':Vector(root[:3])*self.scale};rotations={'root':rotation(root[3:6])};heads={}
        def visit(parent):
            for name in self.children.get(parent,[]):
                bone=self.bones[name];angles=[0,0,0]
                for channel,value in zip(bone['dof'],frame.get(name,())): angles['xyz'.index(channel[1])]=value
                axis=bone['axis'];r=rotations[parent]@axis@rotation(angles)@axis.transposed()
                heads[name]=positions[parent];positions[name]=positions[parent]+r@bone['direction']*(bone['length']*self.scale);rotations[name]=r
                visit(name)
        visit('root')
        return positions,rotations,heads

    def sample(self,index):
        index=max(0,min(len(self.frames)-1,index));a=int(index);b=min(a+1,len(self.frames)-1);t=index-a
        pa,ra,ha=self.cached[a];pb,rb,hb=self.cached[b]
        return ({n:pa[n].lerp(pb[n],t) for n in pa},
                {n:ra[n].to_quaternion().slerp(rb[n].to_quaternion(),t).to_matrix() for n in ra},
                {n:ha[n].lerp(hb[n],t) for n in ha})

    def heading(self,first,last):
        pa,ra,_=self.sample(first);pb,_,_=self.sample(last)
        direction=pb['root']-pa['root'];direction.y=0
        if direction.length<.05: direction=ra['root']@Vector((0,0,1));direction.y=0
        front=WORLD@direction.normalized()
        return Matrix.Rotation(-math.atan2(front.x,-front.y),3,'Z')@WORLD

    def cycle(self,min_seconds,max_seconds):
        """Find matching measured limb poses, away from startup/turning samples."""
        lo=int(min_seconds*self.fps);hi=int(max_seconds*self.fps)
        names=['lfemur','ltibia','lfoot','rfemur','rtibia','rfoot','lhumerus','lradius','rhumerus','rradius','thorax']
        best=None
        for a in range(max(4,int(.12*self.fps)),max(5,min(len(self.frames)-lo-1,int(.8*self.fps))),4):
            pa,_,ha=self.cached[a]
            for b in range(a+lo,min(a+hi,len(self.frames)-1)):
                pb,_,hb=self.cached[b]
                cost=sum(((pa[n]-ha[n]).normalized()-(pb[n]-hb[n]).normalized()).length_squared for n in names)
                if best is None or cost<best[0]: best=(cost,a,b)
        if best is None: raise ValueError('No usable gait cycle in '+self.take)
        print('CMU_CYCLE',self.take,best, 'seconds',(best[2]-best[1])/self.fps,flush=True)
        return best[1],best[2]

    def standing_prefix(self):
        origin=self.cached[0][0]['root'];end=0
        for i,(positions,_,_) in enumerate(self.cached):
            delta=positions['root']-origin;delta.y=0
            if delta.length>.10: break
            end=i
        return 0,max(2,min(end,int(3*self.fps)))

    def summary(self):
        roots=[frame[0]['root'] for frame in self.cached]
        return {'take':self.take,'frames':len(roots),'seconds':len(roots)/self.fps,
                'height':(min(p.y for p in roots),max(p.y for p in roots)),
                'height_samples':[round(roots[min(len(roots)-1,int(i*self.fps/2))].y,3) for i in range(int(len(roots)/self.fps*2)+1)]}
