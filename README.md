# Simulador de Hop-up de Airsoft

Simulação em primeira pessoa da balística de uma BB de airsoft de 6 mm, com o efeito
Magnus gerado pelo hop-up regulável de 1% a 100%.

Trabalho da disciplina **Técnicas Avançadas para Motores de Jogos Digitais**.
Unity 6 (6000.5.8f1) · URP · Input System.

## Como rodar

1. Abra o projeto no Unity 6000.5.8f1.
2. Menu **Airsoft → Construir cena completa** (monta cena, materiais, prefabs e texturas).
3. **Play**.

## Controles

| Comando | Ação |
|---|---|
| Mouse | olhar |
| W A S D / Shift | andar / correr |
| Botão esquerdo | atirar |
| **Roda do mouse** | **regular o hop-up** |
| ↑ ↓ | hop-up fino (1%) |
| F | nivelar o cano |
| M | alternar modelo de Magnus |
| V | câmera lateral (modo análise) |
| R | limpar rastros e marcadores |
| H | mostrar/ocultar ajuda |

## A física

```
v₀ = √(2E/m)              = 122,07 m/s = 400,5 fps   (E = 1,49 J, m = 0,2 g)
F_arrasto = ½·ρ·Cd·A·v²                              (quadrático, oposto ao movimento)
F_magnus  = √v · BackspinDrag                        (perpendicular à velocidade)
```

Documentação técnica completa, com derivações, tabela de calibração e validação
numérica: [`Assets/_Airsoft/LEIAME.md`](Assets/_Airsoft/LEIAME.md).

## Estrutura

```
Assets/_Airsoft/
├── Scripts/
│   ├── BBProjectile.cs      física da BB: arrasto + Magnus
│   ├── AirsoftWeapon.cs     velocidade inicial, disparo, hop-up
│   ├── PlayerController.cs  mouse-look + movimento
│   ├── AirsoftHUD.cs        HUD, medidor de hop-up, histórico
│   ├── Target.cs            alvos com anéis e pontuação
│   ├── ImpactMarker.cs      marcador no ponto de queda
│   └── CameraToggle.cs      primeira pessoa ↔ câmera lateral
├── Editor/
│   └── AirsoftSceneBuilder.cs   monta a cena inteira e gera as texturas
└── LEIAME.md
```

## Créditos

Modelos das armas: [Free Pack - Gun](https://assetstore.unity.com/packages/3d/props/guns/free-pack-gun-308387)
por PolyOne Studio (Unity Asset Store).
