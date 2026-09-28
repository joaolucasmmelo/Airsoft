# Simulador de Airsoft

Jogo de tiro em primeira pessoa feito na Unity 6 (URP + Input System), onde a BB de
6 mm segue uma física real: arrasto do ar e efeito Magnus gerado pelo hop-up.

O objetivo é acertar os alvos que aparecem antes do tempo acabar, administrando a
munição das três armas.

## Como jogar

Atire no botão **START**, na parede atrás de você, para começar a partida.

A cada fase aparecem de 1 a 4 alvos e um tempo para acertar todos. Acertou todos,
passa de fase; a cada fase o tempo diminui e os alvos ficam mais distantes. A partida
acaba quando o tempo esgota ou quando a munição acaba.

Antes do START todas as armas ficam carregadas com munição infinita, para treinar.

## Mecânicas

**Hop-up.** A roda do mouse regula o backspin da BB (de 5% a 100%). Quanto maior,
mais longe a BB vai, porque a força de Magnus segura a queda. A faixa verde do
medidor é a regulagem de referência.

**Três armas, três munições.** Cada arma tem a sua potência de mola, a sua massa de
BB e a sua cadência, então cada uma alcança uma distância diferente:

| Arma | Tecla | Carregador | BB | Potência | Cadência | Alcance |
|---|---|---|---|---|---|---|
| Pistola | 1 | 12 | 0,12 g | 0,5 J | 3 BB/s | ~40 m |
| AK47 | 2 | 25 | 0,20 g | 1,49 J | 8,3 BB/s | ~65 m |
| Sniper | 3 | 6 | 0,45 g | 4,5 J | 0,7 BB/s | ~100 m |

**Munição.** A partida começa só com a pistola carregada. Cada fase concluída tem 50%
de chance de largar um carregador aleatório no chão, que se pega com **E** e só serve
na arma do mesmo tipo. Por isso vale guardar as balas da sniper para os alvos longe e
usar a pistola nos alvos perto.

**Modos de tiro.** A tecla **F** alterna entre semiautomático e automático. Os dois
respeitam a cadência da arma, que é calculada pela rotação do motor.

**Pontos.** Cada acerto vale conforme o anel atingido: centro 10, meio 5, borda 2.

## Controles

| Comando | Ação |
|---|---|
| Mouse | olhar |
| W A S D / Shift | andar / correr |
| Botão esquerdo | atirar |
| 1 / 2 / 3 | pistola / AK / sniper |
| F | modo SEMI / AUTO |
| E | pegar carregador do chão |
| Roda do mouse | regular o hop-up |
| Setas cima/baixo | hop-up fino (1%) |
| Esc | soltar o cursor |

## A física

```
v = √(2E/m)                      velocidade da BB pela energia da mola
F_arrasto = ½·ρ·Cd·A·v²          quadrático, contra o movimento
F_magnus  = ½·ρ·A·k·ω·r·v        perpendicular à velocidade (hop-up)
ROF = RPM / (60 · N)             cadência, em BBs por segundo
```

A explicação completa, com a calibração e a validação numérica, está em
[`Assets/_Airsoft/LEIAME.md`](Assets/_Airsoft/LEIAME.md).

## Como abrir na Unity

1. Abra o projeto na Unity **6000.5.9f1**.
2. Abra a cena `Assets/Scenes/SampleScene.unity`.
3. **Play**.

## Estrutura

```
Assets/_Airsoft/
├── Scripts/
│   ├── BBProjectile.cs       física da BB: arrasto e Magnus
│   ├── AirsoftWeapon.cs      disparo, cadência, hop-up, munição
│   ├── Carregador.cs         tipo, capacidade, quantidade e massa das BBs
│   ├── TipoDeCarregador.cs   enum de compatibilidade arma/carregador
│   ├── CarregadorNoChao.cs   carregador coletável
│   ├── ArmasDoJogador.cs     troca de arma e coleta com E
│   ├── GameManager.cs        fases, tempo, pontos e fim de jogo
│   ├── Target.cs             alvo com anéis
│   ├── StartButton.cs        botão START da parede
│   ├── QuadroDaArma.cs       quadro de informações na parede
│   ├── PlayerController.cs   movimento e mouse-look
│   ├── AirsoftHUD.cs         HUD na tela
│   └── ImpactMarker.cs       marca onde a BB caiu
├── Editor/                   menus que montam a cena, as armas e os quadros
└── LEIAME.md                 documentação técnica
```

## Créditos

Modelos das armas: [Free Pack - Gun](https://assetstore.unity.com/packages/3d/props/guns/free-pack-gun-308387)
por PolyOne Studio (Unity Asset Store).
