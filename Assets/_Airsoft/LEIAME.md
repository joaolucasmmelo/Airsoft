# Simulador de Hop-up de Airsoft — documentação técnica

Simulação em primeira pessoa da balística de uma BB de airsoft de 6mm, com o
efeito Magnus gerado pelo hop-up regulável de 1% a 100%.

---

## 1. Como rodar

1. Abra o projeto no Unity (a compilação dos scripts é automática).
2. No menu superior: **Airsoft → Construir cena completa**.
3. Aperte **Play**.

O menu monta tudo do zero (materiais, prefabs, estande, rig de primeira pessoa
com a AK47 encaixada, HUD) e pode ser executado quantas vezes quiser.

### Controles

| Comando | Ação |
|---|---|
| Mouse | olhar |
| W A S D / Shift | andar / correr |
| Botão esquerdo | atirar |
| **Roda do mouse** | **regular o hop-up** |
| ↑ ↓ | hop-up fino (1% por toque) |
| F | nivelar o cano (elevação 0°) |
| M | alternar modelo de Magnus (simplificado ↔ completo) |
| V | câmera lateral (modo análise) |
| R | limpar rastros e marcadores |
| H | mostrar/ocultar ajuda |
| Esc | soltar o cursor |

> O hop-up é ajustado pela roda do mouse, e não por um slider clicável, porque em
> primeira pessoa o cursor fica travado para o mouse-look funcionar. A barra na
> tela é um **medidor**. Como bônus, girar a roda imita o dial da arma real.

### O estande

A cena montada é um estande de tiro:

- **Chão de grama** — textura gerada proceduralmente pelo próprio script de Editor
  (`T_Grama.png`, 512×512). Usa ruído de Perlin sem costura em três oitavas mais um
  granulado por texel, e é repetida 200 vezes (um ladrilho a cada 3 m).
- **Marcas de distância** a cada 10 m nas laterais do corredor; as de 50 em 50 m são
  mais altas e verdes.
- **8 alvos** com anéis concêntricos, a 10, 20, 30, 40, 50, 60, 75 e 90 m, alternando
  os lados. O corredor central fica livre de propósito, para dar para testar as
  trajetórias longas sem esbarrar em alvo.
- **Paredes**: fundo a 132 m (12 m de altura, contém até os tiros de hop-up
  excessivo), lateral esquerda e uma atrás do atirador. O lado direito fica aberto
  porque é de lá que a câmera de análise enxerga as trajetórias de perfil.

Acertar um alvo vale pontos conforme o anel: **MOSCA** (10), **anel interno** (5),
**anel externo** (2). A placa pisca no impacto e a HUD mostra o placar. `R` zera tudo.

---

## 2. A física

Toda a física está em `Scripts/BBProjectile.cs`, no `FixedUpdate()`.
Três forças agem sobre a BB: **gravidade** (do próprio Unity), **arrasto** e
**sustentação**.

### 2.1 Velocidade inicial

O enunciado dá a energia de saída, não a velocidade. A velocidade é derivada dela:

```
E = ½mv²   →   v₀ = √(2E/m)

v₀ = √(2 × 1.49 J / 0.0002 kg) = 122.07 m/s = 400.5 fps   ✔ bate com os 400fps
```

No código (`AirsoftWeapon.MuzzleSpeed`), a massa é lida do **Rigidbody do prefab
da BB**, não digitada à mão. É por isso que trocar a BB de 0,20g para 0,25g
recalcula a velocidade sozinho — como na vida real, onde a mola é a mesma e só a
munição muda:

| Massa | v₀ | fps | Alcance (hop 55%) |
|---|---|---|---|
| 0,20 g | 122,1 m/s | 400 | 63,6 m |
| 0,25 g | 109,2 m/s | 358 | 62,6 m |
| 0,30 g | 99,7 m/s | 327 | 59,7 m |
| 0,40 g | 86,3 m/s | 283 | 52,4 m |

### 2.2 Arrasto (quadrático)

```
F_arrasto = ½ · ρ · Cd · A · v²        na direção OPOSTA ao movimento

ρ  = 1.225 kg/m³   (ar ao nível do mar)
Cd = 0.47          (esfera lisa)
A  = πr² = π(0.003)² = 2.827×10⁻⁵ m²
```

Na boca do cano isso vale **0,121 N**, ou seja **62× o peso da BB**
(0,00196 N) — uma desaceleração de 606 m/s². É essa brutalidade que explica o
alcance curto do airsoft: sem hop-up nenhum, a BB sai a 122 m/s e chega aos 30 m
já com 36 m/s.

> **Por que não usar o `Linear Damping` do Rigidbody?** Porque ele é *linear*
> (F ∝ v), e o arrasto real de uma esfera é *quadrático* (F ∝ v²). O enunciado
> pede o mecanismo "o mais realista possível", então o damping do Unity fica
> em 0 e o arrasto é calculado à mão.

### 2.3 Sustentação (efeito Magnus)

**Modelo simplificado** (o pedido no enunciado):

```
F_sustentação = √v · BackspinDrag
```

**Modelo completo** (o item opcional — tecla `M` alterna em jogo):

```
F = ½ · ρ · A · Cl · v²      com   Cl = k · S   e   S = ωr/v
  = ½ · ρ · A · k · ω · r · v
```

onde `S` é a razão de spin e `ω` a velocidade angular do backspin. Este modelo
considera a secção transversal da esfera explicitamente e decai **linearmente**
com a velocidade, enquanto o simplificado decai com √v — por isso o completo
produz trajetórias mais conservadoras.

**A direção é o detalhe que mais gera bug.** A força é perpendicular à
velocidade, não simplesmente "para cima":

```csharp
Vector3 eixoBackspin = Vector3.Cross(direcaoVelocidade, Vector3.up);
Vector3 dirSustentacao = Vector3.Cross(eixoBackspin.normalized, direcaoVelocidade);
```

Se fosse `Vector3.up` fixo, a trajetória de muito hop-up subiria para sempre em
linha reta. Como a conta usa a velocidade **atual**, a força acompanha a curva
sozinha: quando a BB sobe, a sustentação inclina junto e para de empurrar para
cima — que é exatamente o que produz a curva vermelha "muito hop-up" do enunciado.

---

## 3. Calibração do hop-up

O mapeamento `hop-up % → BackspinDrag` não foi chutado: foi calibrado integrando
numericamente a trajetória (RK explícito, dt = 0,5 ms) e escolhendo o
`maxBackspinDrag` que reproduz as três curvas do enunciado.

`BackspinDrag = maxBackspinDrag × (hop% / 100)`, com **maxBackspinDrag = 6,5×10⁻⁴**.

| hop-up | BackspinDrag | sustentação / peso (na saída) | alcance | tempo de voo | ápice |
|---|---|---|---|---|---|
| 1 % | 6,5e-6 | 0,04 | 35,7 m | 0,66 s | 1,40 m |
| 25 % | 1,6e-4 | 0,92 | 44,4 m | 1,02 s | 1,40 m |
| 40 % | 2,6e-4 | 1,46 | 52,9 m | 1,53 s | 1,48 m |
| **55 %** | **3,6e-4** | **2,01** | **63,6 m** | **2,46 s** | **2,21 m** |
| 70 % | 4,6e-4 | 2,56 | 74,0 m | 3,85 s | 4,30 m |
| 85 % | 5,5e-4 | 3,11 | 82,0 m | 5,47 s | 8,03 m |
| 100 % | 6,5e-4 | 3,66 | 88,0 m | 6,98 s | 12,13 m |

Isso reproduz as três curvas do documento da atividade:

- **pouco hop-up** (≈1–25 %): a BB despenca cedo, ~35–45 m
- **hop-up ideal** (≈50–60 %): trajetória quase plana, alcance máximo útil
- **muito hop-up** (≈85–100 %): a BB sobe muito, perde velocidade e cai longe

Referência da elevação: todos os números acima são com o **cano nivelado a 0°**.
A HUD avisa quando o cano não está nivelado — 1° de elevação já muda o alcance o
bastante para invalidar a comparação. Use `F` para nivelar antes de comparar.

### Erro de integração

O Fixed Timestep foi baixado de 0,02 s para **0,005 s**. Comparação do alcance
simulado contra uma referência de alta precisão (dt = 0,5 ms):

| hop-up | referência | dt = 0,005 (usado) | dt = 0,02 (padrão do Unity) |
|---|---|---|---|
| 1 % | 35,7 m | 35,1 m | 33,2 m |
| 55 % | 63,6 m | 62,8 m | 60,3 m |
| 100 % | 88,0 m | 87,0 m | 83,7 m |

O padrão do Unity erraria ~5 %. Com 0,005 s o erro cai para ~1 %. Além disso, a
122 m/s a BB anda **2,44 m por passo** no timestep padrão — ela atravessaria o
chão sem registrar colisão (*tunneling*). Por isso também
`Collision Detection = Continuous Dynamic`, que varre o caminho inteiro entre
dois passos em vez de só comparar as posições.

---

## 4. Onde cada requisito da atividade foi atendido

| Requisito | Onde |
|---|---|
| Arma como objeto independente com geometria | `WeaponHolder` + modelo `SM_Ak47` na cena |
| Posição definida da boca do cano | `Muzzle` (auto-posicionado na ponta do cano) |
| BBs instanciadas de um modelo reutilizável | `Prefabs/BB.prefab` |
| Massa 0,20 g | `Rigidbody.mass = 0.0002` no prefab |
| Raio 3 mm | `SphereCollider.radius = 0.003` |
| Arrasto realista | `BBProjectile.FixedUpdate()` — quadrático |
| Clique esquerdo instancia e dispara | `AirsoftWeapon.HandleFireInput()` |
| Velocidade derivada da energia de 1.49 J | `AirsoftWeapon.MuzzleSpeed` |
| Verificação da velocidade no Console | `AirsoftWeapon.Start()` e no 1º disparo |
| Script para a BB | `Scripts/BBProjectile.cs` |
| Variável `BackspinDrag` | `BBProjectile.backspinDrag` |
| Força de sustentação a cada frame | `BBProjectile.FixedUpdate()` |
| `Mathf.Sqrt(velocidade) * BackspinDrag` | `BBProjectile.CurrentLift()` |
| Força perpendicular à direção da BB | `BBProjectile.LiftDirection()` |
| Fórmula completa (opcional) | `MagnusModel.Completo` — tecla `M` |
| Experimentar BackspinDrag e massa | roda do mouse / massa no prefab |
| Distância do impacto | `BBProjectile.Resolve()` + HUD |

---

## 5. Experimentos sugeridos para o relatório

1. **Efeito do hop-up.** Nivele o cano (`F`), atire em 1 %, 25 %, 55 %, 85 % e
   100 %. A HUD registra as distâncias no histórico. Aperte `V` para ver as
   curvas de lado.
2. **Efeito da massa.** Abra `Prefabs/BB.prefab`, mude `Rigidbody → Mass` para
   `0.00025` (0,25 g) e repita. A velocidade inicial cai (mesma energia, mais
   massa), mas a BB mantém melhor a velocidade — mais inércia contra o arrasto.
3. **Efeito do arrasto.** No prefab, desmarque `BBProjectile → Enable Drag`.
   Com hop-up em 1 % e o cano nivelado, o alcance salta de **35,4 m para 65,2 m**.
   O contraste fica ainda mais forte mirando a **45°**: sem arrasto a BB alcança
   **1520 m** (o `v₀²/g` do lançamento balístico ideal), com arrasto apenas
   **58,5 m** — uma redução de 26×. É a demonstração mais direta de que, neste
   problema, o arrasto domina tudo.
4. **Simplificado × completo.** Tecla `M`. Compare o formato das curvas: o
   modelo completo decai com `v`, o simplificado com `√v`.
5. **Alcance útil na prática.** Com o cano nivelado, tente acertar o alvo de 50 m
   variando só o hop-up. Com pouco hop a BB passa por baixo; com hop demais passa
   por cima. Existe uma faixa estreita que acerta — é exatamente o que a regulagem
   do hop-up faz numa arma real, e é o argumento mais concreto do relatório.

---

## 6. Ajustes rápidos

| O que | Onde |
|---|---|
| Força máxima do hop-up | `AirsoftWeapon → Max Backspin Drag` (padrão 6,5e-4) |
| Duração do rastro (ghosting) | `BB.prefab → Trail → Time` (padrão 8 s) |
| Cor do rastro | `AirsoftWeapon → Neon Color` |
| Rastro colorido por hop-up (azul/verde/vermelho) | `AirsoftWeapon → Color By Hop Up` |
| Repetição da grama | `M_Chao → Base Map → Tiling` (padrão 200) |
| Distâncias dos alvos | `AirsoftSceneBuilder.BuildTargets()` — vetor `distancias` |
| Regerar as texturas | apague a pasta `_Airsoft/Textures` e reconstrua a cena |
| Intensidade do brilho neon | Global Volume → Bloom → Intensity |
| Sensibilidade do mouse | `PlayerController → Mouse Sensitivity` |
| Arma cortada / mal posicionada | `WeaponHolder → Transform` (offset da mão direita) |
| Cano ao contrário | menu **Airsoft → Ferramentas → Girar a arma 180°** |
