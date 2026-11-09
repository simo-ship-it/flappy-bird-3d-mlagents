from pathlib import Path
import argparse, os, subprocess, sys, shutil
ROOT = Path(__file__).resolve().parents[1]
(ROOT/"logs").mkdir(exist_ok=True)
trainer = Path(sys.executable).with_name("mlagents-learn")
parser=argparse.ArgumentParser()
parser.add_argument("--run-id",default="flappy3d-final-v7")
parser.add_argument("--resume",action="store_true")
args=parser.parse_args()
run=args.run_id
command = [str(trainer), str(ROOT/"config/flappy-prizes.yaml"),
    "--env="+str(ROOT/"Builds/FlappyBird3D-Training.app"),
    "--results-dir="+str(ROOT/"results"), "--run-id="+run, "--seed=42",
    "--torch-device=cpu", "--no-graphics", "--quality-level=0",
    "--time-scale=20", "--timeout-wait=120"]
if args.resume: command.append("--resume")
else: command.append("--initialize-from=flappy3d-transfer-11obs")
with (ROOT/"logs/training.log").open("a") as log:
    process = subprocess.Popen(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        text=True, env={**os.environ,"OMP_NUM_THREADS":"1"})
    for line in process.stdout:
        log.write(line); log.flush()
        if any(k in line for k in ["Step:","Connected","Exported","ERROR","Traceback","Exception"]):
            print(line,end="",flush=True)
    status = process.wait()
if status: raise SystemExit(status)
model = ROOT/"results"/run/"FlappyBird3D.onnx"
shutil.copy2(model,ROOT/"Assets/Models/FlappyBird3D.onnx")
print("TRAINING COMPLETE; model copied into Assets/Models.",flush=True)

