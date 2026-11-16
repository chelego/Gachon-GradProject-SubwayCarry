"""Extract anonymous body poses and short local tracks from subway reference clips."""
import argparse
import json
from pathlib import Path
import subprocess

import cv2
import mediapipe as mp
import numpy as np
from scipy.optimize import linear_sum_assignment

POSE_SEGMENTS = {
    "000745644": (0, 31),
    "000750332": (0, 34),
    "000753404": (0, 37),
    "000758618": (0, 17),
    "000805406": (12, 46),
    "000810226": (25, 43),
    "000902350": (237, 260),
}
WIDTH, HEIGHT = 360, 640
JOINTS = np.arange(11, 33)  # body only, no facial landmarks


def background_motion(before, current, previous_centers):
    if before is None:
        return np.eye(2, 3, dtype=np.float32), False
    mask = np.full(before.shape, 255, np.uint8)
    for center, size in previous_centers:
        x, y = center.round().astype(int)
        radius = int(size)
        cv2.rectangle(mask, (x-radius, y-2*radius), (x+radius, y+3*radius), 0, -1)
    corners = cv2.goodFeaturesToTrack(before, maxCorners=100, qualityLevel=.016, minDistance=8, mask=mask)
    if corners is not None:
        moved, valid, _ = cv2.calcOpticalFlowPyrLK(before, current, corners, None)
        matched = valid.ravel().astype(bool)
        if np.count_nonzero(matched) >= 12:
            affine, inliers = cv2.estimateAffinePartial2D(corners[matched], moved[matched], ransacReprojThreshold=2.6)
            if affine is not None and np.count_nonzero(inliers) >= 10 and .92 < np.linalg.norm(affine[0,:2]) < 1.08:
                return affine, True
    return np.eye(2, 3, dtype=np.float32), False


def detection(landmarks):
    points = np.asarray([[p.x*WIDTH, p.y*HEIGHT] for p in landmarks], dtype=np.float32)
    visible = np.asarray([min(p.visibility, p.presence) for p in landmarks], dtype=np.float32)
    if np.min(visible[[11,12,23,24]]) < .50:
        return None
    center = (points[23]+points[24])*.5
    shoulder = (points[11]+points[12])*.5
    length = float(np.linalg.norm(center-shoulder))
    if length < 13 or length > 250:
        return None
    return center, length, (points[JOINTS]-center)/length, visible[JOINTS]


def process(path, start, end, fps, model_path, ffmpeg):
    cmd=[str(ffmpeg),'-hide_banner','-loglevel','error','-ss',str(start),'-i',str(path),'-t',str(end-start),
        '-vf',f'fps={fps},scale={WIDTH}:{HEIGHT}:force_original_aspect_ratio=decrease,pad={WIDTH}:{HEIGHT}:(ow-iw)/2:(oh-ih)/2',
        '-an','-pix_fmt','rgb24','-f','rawvideo','-']
    options=mp.tasks.vision.PoseLandmarkerOptions(
        base_options=mp.tasks.BaseOptions(model_asset_path=str(model_path)),
        running_mode=mp.tasks.vision.RunningMode.VIDEO, num_poses=5,
        min_pose_detection_confidence=.48,min_pose_presence_confidence=.50,min_tracking_confidence=.49)
    last_gray=None
    old=[]
    rows=[]
    count=0
    tracking_id=0
    with subprocess.Popen(cmd,stdout=subprocess.PIPE) as video, mp.tasks.vision.PoseLandmarker.create_from_options(options) as pose:
        while raw:=video.stdout.read(WIDTH*HEIGHT*3):
            if len(raw) != WIDTH*HEIGHT*3:
                raise ValueError("Incomplete decoded frame")
            rgb=np.frombuffer(raw,dtype=np.uint8).reshape(HEIGHT,WIDTH,3)
            stamp=round(count/fps*1000)
            frame=mp.Image(image_format=mp.ImageFormat.SRGB,data=rgb)
            gray=cv2.cvtColor(rgb,cv2.COLOR_RGB2GRAY)
            affine,reliable=background_motion(last_gray,gray,[(a[1],a[2]) for a in old])
            found=[x for landmarks in pose.detect_for_video(frame,stamp).pose_landmarks if (x:=detection(landmarks)) is not None]
            matched={}
            if old and found:
                prior=np.stack([affine[:,:2]@a[1]+affine[:,2] for a in old])
                next_center=np.stack([a[0] for a in found])
                scales=np.asarray([a[2] for a in old])[:,None]
                actual=np.asarray([a[1] for a in found])[None,:]
                cost=np.linalg.norm(prior[:,None]-next_center[None,:],axis=-1)/np.maximum(scales,actual)
                cost+=np.abs(np.log(actual/scales))*.68
                ri,ci=linear_sum_assignment(cost)
                for i,j in zip(ri,ci):
                    if cost[i,j]<.85:matched[j]=(old[i][0],prior[i],old[i][2])
            current=[]
            for j,(center,scale,xy,vis) in enumerate(found):
                link=matched.get(j)
                speed=None
                if link is None:
                    tracking_id+=1
                    track=tracking_id
                else:
                    track,previous,old_scale=link
                    if reliable:
                        speed=((center-previous)*fps/np.sqrt(scale*old_scale)).tolist()
                current.append((track,center,scale))
                rows.append(dict(t=round(start+count/fps,3),track=int(track),speed=speed,scale=round(scale,2),
                                 joints=np.round(np.column_stack((xy,vis)),4).tolist()))
            last_gray=gray
            old=current
            count+=1
        if video.wait()!=0:raise RuntimeError(f"Decoding failed: {path.name}")
    return count,rows


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--files",type=Path,required=True)
    parser.add_argument("--model",type=Path,required=True)
    parser.add_argument("--ffmpeg",type=Path,required=True)
    parser.add_argument("--output",type=Path,required=True)
    parser.add_argument("--photo",type=Path,nargs="*",default=[])
    parser.add_argument("--fps",type=int,default=5)
    args=parser.parse_args()
    data={}
    for video_id,(begin,end) in POSE_SEGMENTS.items():
        path=next(args.files.glob("*"+video_id+".mp4"))
        count,observations=process(path,begin,end,args.fps,args.model,args.ffmpeg)
        data[video_id]=dict(frames=count,rows=observations)
        print(video_id,count,len(observations),flush=True)
    image_options=mp.tasks.vision.PoseLandmarkerOptions(base_options=mp.tasks.BaseOptions(
        model_asset_path=str(args.model)),num_poses=5)
    with mp.tasks.vision.PoseLandmarker.create_from_options(image_options) as detector:
        for photo_index,path in enumerate(args.photo,1):
            image=mp.Image.create_from_file(str(path))
            poses=detector.detect(image)
            data[f"photo{photo_index}"]=dict(poses=len(poses.pose_landmarks))
            print(f"photo{photo_index}",len(poses.pose_landmarks),flush=True)
    args.output.write_text(json.dumps(data,separators=(",",":")),encoding="utf-8")

if __name__=="__main__":
    main()

