# PRs pendentes: KiritoJPK_fixes (sessão de 20–21/10/2026)

Todas as mudanças abaixo estão no PC (E:\GOF2_Remake\Galaxy-on-Fire-2-Unity-Remake) e ainda não foram para o upstream.
Base atual: o merge de origin/main feito em 20/10 (`atualizar_upstream.bat` / `finalizar_merge.bat`).

## A. Branches já enviadas ao fork (falta só abrir o PR no GitHub)

| Branch | O que faz | Arquivo |
|---|---|---|
| `free-look-invert-y` | Visão livre (T): cima/baixo invertidos (mouse, touch, analógico); a opção "Inverter órbita Y" desfaz | `Space/FreeLookCamera.cs` |
| `ship-preview-no-reflections` | Miniatura da nave (info / hangar) sem cubemap, reflexo nem rim light | `App/UI/ShipPreview.cs` |
| `cronus-engine-pods` | Cápsulas do motor da Cronus (part0/part1): X, Y, rot Z; Z animado -18.7..-20.7 / -20.9..-18.9 | `Models/valkyrie/ships/v_ship_037_deep_science.gof2mesh.json` |

Links de comparação: `https://github.com/JoppieToppie/Galaxy-on-Fire-2-Unity-Remake/compare/main...KiritoJPK:Galaxy-on-Fire-2-Unity-Remake:<branch>`

## B. Enviadas em 21/10 (aguardando o dev)

| Branch | O que faz |
|---|---|
| `interface-overhaul` | PR "Interface": toda a mudança das interfaces (menu principal, estação/hangar, voo), PR 1 a 5 + 5b abaixo, com as traduções nos 13 idiomas |
| `star-map-items-hidden-until-placed` | PR "Mapa: caixas sobrepostas" (StarMap.cs MakeItem / Place) |
| `hangar-sky-cubemap-batch` | PR 6, ferramenta de cubemaps (+ a lista no Tools Overview) |

O detalhe de cada parte continua abaixo, para referência.

Atenção: `StationMenu.cs`, `GoF2Common.uss`, `MainMenu.cs` e `StarMap.cs` aparecem em mais de uma parte. Para a PR do mapa é
preciso separar por trechos (o split por hunks, como antes). Os PNG novos são LFS: vão por bundle ou são enviados por você.

### PR 1: Dicas de botões (caixas com borda, texto embaixo da tecla)
- Todas as dicas: caixa com borda cinza; teclas em cima e texto embaixo (`UI/GoF2InputGlyphs.uss`: `.hint`, `.hint-keys`, `.hint-label`).
- As teclas numa linha própria (`hint-keys`) em `App/UI/FlightHud.cs`, `App/UI/OrbitViewUi.cs` e `App/UI/StarMap.cs` (StationMenu já tinha).
- Barra de dicas do mapa organizada como a do hangar (`UI/StarMap/StarMap.uss`).
- Rodapé do menu principal ("↑ ↓ NAVEGAR / ENTER SELECIONAR / ESC VOLTAR") como dicas em caixa, fonte 15 px; com controle mostra D-pad/A/B (`App/UI/MainMenu.cs` BuildFooterHints, `UI/MainMenu/MainMenu.uxml` + GoF2InputGlyphs.uss, `UI/MainMenu/MainMenu.uss` `.footer-hints`).
- ESC no menu principal sempre volta (fallback EscBack quando nada tem foco); ESC/B fecha primeiro uma lista de opção aberta (`MainMenu.cs`).

### PR 2: Menu de opções (ícone ?, listas de seleção, barras)
- Ícone **?** (novo `Resources/GoF2Hud/option_info.png`) na ponta direita da linha das opções com descrição; janela abre após 1 s de mouse em cima, ou com um clique/toque; some ao sair, tocar fora, trocar de aba (novo `App/UI/OptionInfo.cs`). Com controle, a linha de descrição aparece no lugar.
- Todas as escolhas viram caixa de seleção (drop-down) à direita, 23% da largura (mín. 150 px); a lista (novo `App/UI/ChoicePopup.cs`, mín. 340 px, alinhada à direita, rola acima de 8 itens, atual em âmbar com ✓); texto longo desliza ida e volta e recomeça ao reabrir; sem animação no texto (`App/UI/ChoiceRow.cs`, `App/UI/OptionControl.cs`).
- Barras (sliders): nome à esquerda; caixa de valor só com contorno, 78 px fixos, que não reage a clique; barra com a largura das caixas de seleção; na aba Som, alto-falante que muta/desmuta (novos `Resources/GoF2Hud/option_sound.png`, `option_sound_muted.png`).
- Estilos: `UI/GoF2Common.uss` (option-info, choice-dropdown/popup, option-slider*, `.option-item--info-room`, transições só de cor).
- Aba Idioma com a mesma altura das outras (`UI/MainMenu/MainMenu.uss` `#languagePage`).
- Janelas do menu principal centralizadas na vertical (`MainMenu.uss` `.panel-host` padding-top 0).

### PR 3: Barra de rolagem em todas as janelas
- Novo `App/UI/ScrollBars.cs`: a barra do hangar em toda ScrollView vertical; aparece só quando o conteúdo passa da altura; fica do lado de fora, à direita (não mexe no conteúdo); opt-out com a classe `gof-no-scroll-bar`; a lista do hangar fica de fora.
- `ScrollBars.Watch(root)` em `MainMenu.cs`, `StationMenu.cs`, `FlightHud.cs`, `StarMap.cs`; estilos `.gof-scroll-bar*` em `GoF2Common.uss`.

### PR 4: Menus da estação
- Sala espacial (lounge): botão Voltar no estilo do Voltar do mapa (`UI/Station/Lounge.uss`); sem a dica ESC VOLTAR; menu do hangar escondido na hora durante a animação de entrada (classe `lounge-intro`) (`StationMenu.cs`, `Lounge.uss`).
- Missões: sem título; abas no estilo das abas da loja; ordem Missões / Atividade pirata / Most Wanted; aba Missões sempre visível; Q/E e LB/RB trocam de aba (`App/UI/MissionsWindow.cs` StepTab, `UI/Station/StationMenu.uxml`, `Lounge.uss`, `StationMenu.cs`).
- Com o mouse, nada fica marcado sozinho, e o item desmarca quando o mouse sai (pointerDriven, UnmarkLeftButton, Select) (`StationMenu.cs`; equivalente em `MainMenu.cs` com PointerLeave em HookFocusSound). Teclado e controle continuam marcando.
- Som de passar o mouse em botões, linhas de opção e abas; som ao navegar com setas e controle; som de clique nas abas (Opções e Debug) (`StationMenu.cs`).
- Menu (MENU): sem "Iniciar novo jogo" e "Sobre"; Voltar no canto superior direito como o da loja (`.system-menu-close`); título "OPÇÕES" branco como "MENU" (`StationMenu.cs`, `UI/Station/StationMenu.uss`).
- Debug: abas acessíveis com as setas (cima a partir da 1ª linha; esquerda/direita trocam; baixo volta) (`StationMenu.cs`).
- Debug: nomes das grades numa linha, centralizados e deslizando quando longos (novo `App/UI/TextMarquee.cs`, `App/UI/CheatsCatalog.cs`, `GoF2Common.uss` `.debug-item-name-clip`); predefinições mostram os itens só ao clicar (não ao passar o mouse); sem **?** nas linhas de grade; predefinições de 8 para 14 vagas (`Space/ShipPresets.cs`).
- Loja: ícone **i** à esquerda de MENU com os controles da loja; a faixa de dicas embaixo da janela some (`StationMenu.cs` ShopHintsContent, `StationMenu.uss` `.station-keys`).

### PR 5: Ícones i / ? no mapa, Inspecionar nave e Action Freeze
- Mapa: **?** com a dica do mapa (628/631) e **i** com as teclas, no topo à direita, do tamanho do título; a barra de dicas de baixo fica invisível (`StarMap.cs` SetHelp/HintsContent, `StarMap.uss`); também no mapa do "Exibir mapa" das Missões (`StationMenu.MapHelpText`, `MissionsWindow.cs`). Novo `Resources/GoF2Hud/option_keys.png`.
- `OptionInfo.cs`: aceita conteúdo próprio (as dicas) e ícone próprio; abre também com clique/toque.
- Inspecionar nave e Action Freeze: **i** no canto superior direito; os itens clicáveis (esconder UI, captura, câmera livre, configurações, voltar) funcionam dentro da janela; a fileira de dicas fica escondida (`StationMenu.cs`, `App/UI/PhotoMode.cs`, `GoF2Common.uss` `.orbit-view-keys`).
- Estilo da janela das dicas (`.hints-popup`) em `GoF2InputGlyphs.uss`: no tema geral, perdia para o `.hint` desse arquivo.

### PR 5b: Menus de voo, debug, menu principal e sons (21/10)
- Menu de pausa e menu de piloto automático / ações: com o mouse só fica marcado o item sob o cursor (nada sobre o vazio); setas / controle trazem a marcação de volta (o primeiro toque só a mostra se o mouse estava no vazio); som ao mover com as setas, ao trocar de aba com Q/E e ao confirmar com Enter (`App/UI/PauseMenu.cs`, `App/UI/FlightHud.cs`; `OptionsView.Select(item, scrollTo)` em `App/UI/OptionsView.cs`).
- Som de passar o mouse só nas abas (loja, Missões, Status, Opções, Debug) e nos botões laterais (estação, coluna do menu principal); o resto sem som (novo `App/UI/UiHover.cs`; `StationMenu.cs`, `MainMenu.cs` HookFocusSound, `PauseMenu.cs`). Setas e controle continuam com som em tudo.
- Clique com o som do menu principal (só o "release", no clique; sem o "push" ao apertar; um por quadro) em todos os botões e caixas de seleção da estação e do voo, menos dentro da loja (`UiHover.ClickSounds/SilentPress`, `StationMenu.Play`, `FlightHud.PlayButton/ButtonSounds`); abas da loja com o mesmo som (`App/UI/HangarWindow.cs`).
- Novo `App/UI/MenuKeys.cs`: ícones **i** (teclas) e **?** (ajuda) para qualquer menu, e o conteúdo padrão (SELECIONAR, ALTERAR VALOR / MOVER / ALTERAR, TROCAR ABA Q/E ou LB/RB, CONFIRMAR, VOLTAR).
- Debug (estação e pausa): **i** e **?** à direita das abas no lugar do texto "Q ◂ ▸ E" (removido também do `GoF2Common.uss`); o **?** explica cada aba (novo texto `debugHelp`); na estação o **i** desce 1 px para alinhar com o **?**.
- **i** com as teclas também na linha de abas das Opções do jogo (`OptionsView.cs`) e ao lado do **?** em Missões e Status (`MissionsWindow.cs`, `StatusWindow.cs`).
- Menu principal: dicas do rodapé removidas; **i** no canto superior direito (só no menu), com Q/E e ←/→ quando as Opções estão abertas (`MainMenu.cs` KeysContent; estilos `.footer-hints` removidos do `MainMenu.uss`).
- Debug do menu principal: nomes longos das trapaças quebram linha em vez de passar por cima da caixa (`MainMenu.uss`).
- Menu (MENU) da estação: sem a faixa de dicas embaixo (`StationMenu.cs` BuildHints).
- Textos novos em `Resources/GoF2Localization/extra_pt.json`: `hudSwitchTab`, `hudChangeValue`, `hudMoveChange`, `debugHelp`.

### PR 6: Ferramenta de cubemaps de reflexo em lote
- `GoF2/Tools/Capture All Sky Reflection Cubemaps`: gera os 19 cubemaps de céu (cubemap_skybox_000..018) direto das camadas assadas (Resources/GoF2Sky), sem visitar os sistemas; funciona no Edit ou no Play mode.
- `GoF2/Tools/Capture All Hangar Reflection Cubemaps`: no Play mode, atracado em qualquer estação, monta e captura em sequência os hangares Terran, Vossk, Nivelian, Midorian, Deep Science e Battlestation, e depois volta ao hangar original.
- Arquivos: `Assets/Scripts/Editor/HangarCubemapCapture.cs` e `Space/World/StationLevel.cs` (RebuildHangarAs / RestoreHangar).

## C. Arquivos novos (lembrar de incluir os .meta gerados pelo Unity)
- `Assets/Scripts/Runtime/App/UI/OptionInfo.cs`, `ChoicePopup.cs`, `ScrollBars.cs`, `TextMarquee.cs`, `MenuKeys.cs`, `UiHover.cs`
- `Assets/Resources/GoF2Hud/option_info.png`, `option_keys.png`, `option_sound.png`, `option_sound_muted.png` (LFS)

## D. Fora do PR
- Script Blender `aem_folder_to_fbx.py` (ferramenta local, não vai para o repo).
- `PR_PENDENTES.md` (este arquivo) está em `.git/info/exclude` e não é commitado.
