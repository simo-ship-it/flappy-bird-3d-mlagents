from pathlib import Path
import json,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[1]
(ROOT/"logs").mkdir(exist_ok=True)
seed=int(sys.argv[1]) if len(sys.argv)>1 else 2026
seconds=35
frames=ROOT/"report/video-frames"
if frames.exists():raise SystemExit("The video frame directory already exists.")
exe=next((ROOT/"Builds/FlappyBird3D.app/Contents/MacOS").iterdir())
command=[str(exe),"-screen-width","1280","-screen-height","720","-screen-fullscreen","0",
    "-logFile",str(ROOT/"logs/video.log"),"--capture",str(frames),
    "--capture-frames",str(seconds*30),"--seed",str(seed)]
subprocess.run(command,cwd=ROOT,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT,check=True,timeout=180)
images=sorted(frames.glob("*.png"))
assert len(images)==seconds*30, len(images)
video=ROOT/"consegna/FlappyBird3D-demo.mp4"
subprocess.run(["/opt/homebrew/bin/ffmpeg","-hide_banner","-loglevel","error","-y",
    "-framerate","30","-i",str(frames/"%05d.png"),"-c:v","libx264","-preset","medium",
    "-crf","20","-pix_fmt","yuv420p","-movflags","+faststart",str(video)],check=True)
probe=subprocess.check_output(["/opt/homebrew/bin/ffprobe","-v","error","-show_entries",
    "stream=width,height,nb_frames,r_frame_rate:format=duration,size","-of","json",str(video)],text=True)
info=json.loads(probe)
assert abs(float(info["format"]["duration"])-seconds)<0.1
shutil.copy2(images[600],ROOT/"consegna/anteprima.png")
meta=json.loads((frames/"recording.json").read_text()); meta["seed"]=seed
(ROOT/"report/video-recording.json").write_text(json.dumps({"game":meta,"media":info},indent=2))
print(json.dumps(meta,indent=2))
print(video)

