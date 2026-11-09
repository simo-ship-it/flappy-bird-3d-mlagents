# Flappy Bird 3D

Flappy Bird 3D del Lab 3, esteso per l'esercizio facoltativo di Unity ML-Agents.
L'agente è addestrato con PPO: attraversa i tubi e raccoglie le sfere dorate
che aggiungono vite.

[![Una partita dell'agente](consegna/anteprima.png)](consegna/FlappyBird3D-demo.mp4)

[Video di 35 secondi](consegna/FlappyBird3D-demo.mp4) ·
[Relazione](report/Relazione-FlappyBird3D.md)

## Aprire il progetto

Il progetto usa **Unity 6000.6.2f1** e **ML-Agents 4.2.0**.
Clona il repository, aggiungi la cartella in Unity Hub con
**Add > Add project from disk** e apri una delle scene in Assets/Scenes:

| Scena | Uso |
|---|---|
| Inference.unity | Osservare l'agente già addestrato |
| Lab3Tutorial.unity | Giocare con frecce e barra spaziatrice |
| Training.unity | Allenare l'agente nei 24 ambienti |

Premi Play per avviare la scena. Le arene si attivano all'avvio.
Il modello ONNX è già incluso in Assets/Models; per eseguirlo in Unity
non serve avviare Python.

## Regole

Si parte con una vita. Una sfera dorata ogni tre coppie di tubi ne aggiunge una;
una collisione ne consuma una. Se restano vite, il volo riparte mantenendo il
punteggio. Un premio già raccolto non ricompare alla ripartenza.

Il gioco manuale mantiene il movimento libero del Lab 3. Con l'agente, il volo
resta sul piano verticale e i tubi vanno attraversati passando nei varchi.
La rete usa 11 osservazioni su posizione, velocità, tubi e premi e sceglie se
battere le ali. Ricompense e prove di addestramento sono descritte nella relazione.

Gli asset Sparrow e Pipe sono quelli forniti nel corso. I materiali importati
usano lo shader Standard; il pavimento è marrone uniforme.

## Risultati

Il modello ha raggiunto 40 tubi in tutte le 30 partite del test, senza collisioni,
con una media di circa 13 premi. Il confronto casuale non ha raggiunto 40 tubi
in nessuna delle 30 partite. I dati delle singole prove e i grafici sono in report.

La valutazione usa seed base 13007 e un limite di 120 secondi simulati.
Il limite di 40 tubi serve al test: nel gioco normale la partita continua
finché restano vite.

## Ripetere l'allenamento

Con un ambiente Python 3.10.12 attivo:

```bash
python -m pip install -r requirements.txt
```

Dal menu Unity **Flappy Lab > Prepare tutorial and training** compila
l'app di training per Mac. Poi, dalla cartella del progetto:

```bash
python scripts/prepare_transfer.py
python scripts/train.py --run-id flappy3d-replica
```

Il checkpoint v6 necessario al trasferimento è incluso. Il primo script prepara
la rete a 11 osservazioni; il secondo la allena con config/flappy-prizes.yaml.
Per riprendere una replica interrotta, aggiungi --resume con lo stesso run-id.
**Flappy Lab > Build trained game** ricompila il gioco con il modello aggiornato.

Per ripetere i controlli, dopo aver compilato le app per Mac:

```bash
python scripts/verify_game.py mechanics
python scripts/verify_game.py random
python scripts/verify_game.py trained
```

Nel repository sono inclusi sorgenti, modello, configurazioni, risultati e video.
Unity ricrea le cartelle di cache e le build rimangono locali.
