"""Remove the life-count input while preserving the policy at one life.

The normalized constant's contribution is folded into the first-layer bias.
Both actor and critic are transferred. Optimizer moments start fresh.
"""
from pathlib import Path
import copy
import json
import torch
import torch.nn.functional as F

ROOT = Path(__file__).resolve().parents[1]
source = ROOT / "results/flappy3d-final-v6/FlappyBird3D/checkpoint.pt"
destination = ROOT / "results/flappy3d-transfer-11obs/FlappyBird3D/checkpoint.pt"
original = torch.load(source, map_location="cpu", weights_only=False)
result = {k: copy.deepcopy(v) for k, v in original.items()
          if k in ["Policy", "Optimizer:critic", "global_step"]}
keep = [i for i in range(12) if i != 10]
normalizer = "network_body.observation_encoder.processors.0.normalizer."
first = "network_body._body_endoder.seq_layers.0."

def hidden(state, inputs):
    n = normalizer
    x = ((inputs-state[n+"running_mean"]) /
         (state[n+"running_variance"]/state[n+"normalization_steps"]).sqrt()).clamp(-5,5)
    x = F.silu(F.linear(x,state[first+"weight"],state[first+"bias"]))
    p = "network_body._body_endoder.seq_layers.2."
    return F.silu(F.linear(x,state[p+"weight"],state[p+"bias"]))

torch.manual_seed(73)
inputs = torch.rand(10000,12)*2-1
inputs[:,10] = 0.5
errors = {}
for module in ["Policy", "Optimizer:critic"]:
    state = result[module]
    n = normalizer
    constant = ((0.5-state[n+"running_mean"][10]) /
                (state[n+"running_variance"][10]/state[n+"normalization_steps"]).sqrt()).clamp(-5,5)
    state[first+"bias"] = state[first+"bias"] + state[first+"weight"][:,10]*constant
    state[first+"weight"] = state[first+"weight"][:,keep]
    for key in list(state):
        if key.endswith(("normalizer.running_mean", "normalizer.running_variance")):
            state[key] = state[key][keep]
    before = hidden(original[module],inputs)
    after = hidden(state,inputs[:,keep])
    errors[module] = float((before-after).abs().max())
    assert torch.allclose(before,after,atol=0.0001,rtol=0.0001), errors[module]

p = "action_model._discrete_distribution.branches.0."
old_logits = F.linear(hidden(original["Policy"],inputs),original["Policy"][p+"weight"],original["Policy"][p+"bias"])
new_logits = F.linear(hidden(result["Policy"],inputs[:,keep]),result["Policy"][p+"weight"],result["Policy"][p+"bias"])
same_actions = bool(torch.equal(old_logits.argmax(-1),new_logits.argmax(-1)))
assert same_actions
destination.parent.mkdir(parents=True,exist_ok=True)
torch.save(result,destination)
report = {"source":str(source.relative_to(ROOT)),"destination":str(destination.relative_to(ROOT)),
          "old_observations":12,"new_observations":11,"removed_index":10,
          "fixed_original_life_feature":0.5,"tested_vectors":len(inputs),
          "max_hidden_error":errors,"same_deterministic_actions":same_actions}
(ROOT/"report/transfer-check.json").write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
