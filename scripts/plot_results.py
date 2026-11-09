import json,sys
from pathlib import Path
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
flight = "--flight" in sys.argv
balanced = "--balanced" in sys.argv
name = "flight-training" if flight else ("balanced-training" if balanced else "training")
data=json.loads((ROOT/"report"/("flight-training_metrics.json" if flight else ("balanced-training_metrics.json" if balanced else "training_metrics.json"))).read_text())
fig,axes=plt.subplots(2,3,figsize=(15,8),layout="constrained")
panels=[("Game/PipesPassed","Pipe pairs cleared","Pipe pairs per episode"),
("Game/SurvivalSeconds","Survival","Seconds"),
("Environment/Cumulative Reward","Cumulative reward","Reward"),
("Game/PrizesCollected","Prizes / extra lives","Prizes per episode"),
("Game/Collisions","Collisions","Collisions per episode"),
("Game/Reached40Pipes","Runs reaching 40 pipe pairs","Percent")]
for ax,(tag,title,ylabel) in zip(axes.flat,panels):
    values=data.get(tag,[])
    if not values:continue
    x=np.array([e["step"] for e in values])/1000
    y=np.array([e["value"] for e in values])
    if tag=="Game/Reached40Pipes":y*=100
    ax.plot(x,y,color="#19836f",alpha=.25,lw=1)
    n=min(5,len(y))
    ax.plot(x[n-1:],np.convolve(y,np.ones(n)/n,mode="valid"),color="#087568",lw=2)
    ax.set(title=title,xlabel="Training decisions (thousands)",ylabel=ylabel)
    ax.spines[["top","right"]].set_visible(False);ax.grid(alpha=.2)
    if tag=="Game/Reached40Pipes":ax.set_ylim(0,103)
fig.suptitle("Flappy Bird 3D - " + ("learning to fly" if flight else ("higher collision penalty" if balanced else "flight, prizes and extra lives")),fontsize=18,fontweight="bold")
fig.supxlabel("24 parallel environments, seed 42. Solid line: 5-point moving average; faint line: raw values.",fontsize=10)
fig.savefig(ROOT/"report"/(name+".png"),dpi=160)
print(ROOT/"report"/(name+".png"))

