# Flappy Bird 3D with ML-Agents

The game builds on Flappy Bird 3D from Lab 3 and adds the agent required by the
optional Lab 6 assignment. It uses Sparrow_LOD0, scaled to (6, 3, 5), and the
pipes from Pipe-2.unitypackage. These are the assets provided in the course.

## Game and agent

Movement uses the tutorial's Character Controller, gravity and air-flap impulse.
The camera follows the bird in LateUpdate. Pipe spawning, scoring, prizes and
life management have been added.

Lab3Tutorial keeps the manual controls. In Training and Inference, flight is
restricted to a vertical plane so the agent cannot go around the pipes in depth.
The pipe colliders reach the floor. An additional boundary check prevents the
Character Controller from being pushed through an obstacle during depenetration.
Imported materials use the Standard shader, and the floor is plain brown.

Each run starts with one life. A golden orb appears every three pipe pairs and
grants one extra life when collected. Each collision costs one life. If any lives
remain, the bird restarts while keeping its score. Collected prizes do not reappear
after a restart. One point is awarded for each pipe pair, after entering the
Middle Pipe trigger and fully clearing the gap.

The agent uses 11 observations: height and vertical velocity; distance and height
offset to the next gap; gap size; distance and height offset to the second gap;
distance, height offset and presence of the next prize; and pipe speed.
The action is discrete, with two values: flap or wait.

## Training

The final model uses PPO with seed 42 and 24 parallel environments. The network
has two layers of 128 units and normalizes its observations. The main parameters
are an initial learning rate of 0.0003, gamma 0.99, batch size 1024 and buffer
size 8192. The agent makes a decision every 5 physics steps. Episodes last up to
4,000 steps, or 80 simulated seconds. The final summary is at 500,000 decisions.

| Event | Reward |
|---|---:|
| Pipe pair cleared | +1 |
| Prize collected | +5 |
| Decision while alive | +0.01 |
| Collision | -10 |
| Last life lost | an additional -3 |

An alignment incentive helps the agent learn to collect prizes. When the next
gap contains a prize and is less than 18 units away, each decision receives
`0.3 * exp(-vertical_error² / 4)`. Reward values, initial lives and prize frequency
are project choices; the assignment leaves these details open.

The early policies learned to clear the pipes but collected few prizes. The
flight checkpoint at about 750,000 steps reached 40 pipe pairs in all 30 evaluation
runs, but collected only one prize in total. Rewards and observations were then
changed over the following phases.

| Phase | Observations | Main change |
|---|---:|---|
| flappy3d-ppo-v2 | 12 | Flight training; prize +0.5, collision -2; stopped at about 1.27 million decisions |
| flappy3d-final-v5 | 12 | Prize +5 and collision -10; 750,000 decisions |
| flappy3d-final-v6 | 12 | Alignment +0.05 and 1-3 initial lives during training; 500,000 decisions |
| flappy3d-final-v7 | 11 | Life count removed from observations and alignment increased to +0.3; final model |

In the 12-observation experiments, flight control deteriorated after collecting
a prize. The life-count observation was therefore removed from the final network.
To preserve the learned flight policy, scripts/prepare_transfer.py transfers the
actor and critic from v6: it folds the contribution corresponding to one life
into the first-layer bias, then removes the associated input column and
normalization statistics. PPO training resumes from this transferred network.

The transfer was checked on 10,000 input vectors. Deterministic actions
matched before and after the change. The maximum activation error was
4.8e-06 for the actor and
3.3e-06 for the critic.
Configuration files for the intermediate phases are kept in config.

Three runs were discarded: diagnostic-v1-rail-test allowed lateral escape;
diagnostic-v3-prize-respawn made collected prizes reappear; and flappy3d-final-v4
reached 40 pipe pairs while losing one life after each prize. The fixes prevent
repeated collection and increase the collision penalty. Cumulative rewards from
different phases cannot be compared directly because the reward rules changed.

![Learning to fly](flight-training.png)

![Increasing the collision penalty](balanced-training.png)

![Final training with 11 observations](training.png)

## Results

Evaluation runs the ONNX model in Unity without the Python trainer. It includes
30 runs with base seed 13007, separate from the development seeds
(base 2026). Each run starts with one life and ends at 40 pipe pairs or after
120 simulated seconds. The random baseline flaps with probability 0.2 at each
decision.

| Measure | Random baseline | Trained agent |
|---|---:|---:|
| Runs reaching 40 pipe pairs | 0/30 | 30/30 |
| 40 pipe pairs without collisions | 0/30 | 30/30 |
| Mean pipe pairs per run | 0.17 | 40.00 |
| Mean simulated duration, seconds | 2.29 | 73.43 |
| Mean prizes per run | 0.00 | 12.97 |
| Mean collisions per run | 1.00 | 0.00 |

These results describe this training run and these 30 evaluation runs. Individual
results are in evaluation_random.json and evaluation_trained.json; the CSV files
contain the TensorBoard metrics. The 40-pair limit is only for evaluation.
The normal game continues as long as lives remain.

The game-mechanics checks passed: extra lives, one-time prize collection even
after a restart, one-time scoring, life loss and reset after game over.
The video lasts 35 seconds (1280 x 720, 30 fps, seed 2026) and shows
18 pipe pairs cleared, 6 prizes and 0 collisions.

Model SHA-256: `1442ed0aba1df28e0cb79cc06d04a925846b078a2ef93ce4a07be5ff708a24ba`.

## Submission files

The description and video are in consegna. The TXT file contains
the links to the GitHub repository and video for submission on Campus Virtual.
