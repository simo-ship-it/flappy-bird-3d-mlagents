from pathlib import Path
import json, subprocess, sys
ROOT = Path(__file__).resolve().parents[1]
(ROOT/"logs").mkdir(exist_ok=True)
kind = sys.argv[1]
app = ROOT/"Builds"/({"mechanics":"FlappyBird3D-Tutorial.app",
    "random":"FlappyBird3D-Training.app","trained":"FlappyBird3D.app"}[kind])
executable = next((app/"Contents/MacOS").iterdir())
output = ROOT/"report"/("mechanics.json" if kind=="mechanics" else "evaluation_"+kind+".json")
command = [str(executable),"-batchmode","-nographics","-logFile",str(ROOT/"logs"/(kind+".log"))]
if kind=="mechanics": command += ["--mechanics-check",str(output)]
else:
    command += ["--evaluation",str(output),"--episodes","30","--target-pipes","40","--seed","13007"]
    if kind=="random": command.append("--random")
subprocess.run(command,cwd=ROOT,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT,check=True,timeout=240)
result = json.loads(output.read_text())
print(json.dumps({k:v for k,v in result.items() if k!="trials"},indent=2))
if kind=="mechanics": assert result["passed"]
else: assert result["episodes"]==len(result["trials"])==30

