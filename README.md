# TriAxis Backup & Editor

**Português** · [English](README.en.md)

Aplicativo Windows (C# / .NET 8 / WPF) para o Mesa/Boogie TriAxis. Ele faz:
- **backup e restauração da memória via MIDI SysEx**;
- uma **biblioteca local** de backups;
- a **visualização** dos dumps;
- um **editor visual** no estilo do painel do TriAxis;
- um **modo controle** que transforma o PC numa pedaleira MIDI (canais/programas, bancos, botões CC e entradas MIDI aprendidas).

O editor usa um mapa SysEx **preliminar**, cuja ordem de parâmetros foi conferida com os presets de fábrica do manual oficial, mas ainda não cada valor em todos os aparelhos. Ele sempre gera um arquivo novo e nunca altera o backup original (veja [docs/SYSEX.md](docs/SYSEX.md)).

**Download:** baixe o `TriAxisBackupEditor.exe` na página [Releases](https://github.com/palinetes/TriAxisBackupEditor/releases). É um arquivo único, sem instalação, para Windows 10/11 64 bits.
Esta é uma **versão beta**: está sendo testada com o TriAxis real. Encontrou um problema ou tem uma ideia? Abra uma [issue](https://github.com/palinetes/TriAxisBackupEditor/issues).

**Autor:** @palinetes · **Licença:** [MIT](LICENSE) · **Manual do usuário:** [docs/manual/index.html](docs/manual/index.html) ([English](docs/manual/index.en.html)), também dentro do programa (botão **Manual** ou tecla **F1**, funciona sem internet).

**Idioma:** o programa é bilíngue (português / inglês). Na primeira vez usa o idioma do Windows e dá para trocar no topo da janela. Os documentos técnicos de `docs/` (SYSEX, ROADMAP, plano de testes) estão em português.

## Modo simples e modo avançado

O programa tem uma chave **Simples | Avançado** no topo.

- **Modo simples** (padrão, para quem não entende de MIDI):
  - **Assistente de ligação** na primeira vez, em 4 passos:
    1. escolher o aparelho (entrada e saída juntas);
    2. ver o desenho dos cabos;
    3. fazer o **primeiro backup**, que também testa a ligação (se nada chegar, ele manda inverter os plugues);
    4. descobrir o **número do TriAxis** (o canal): **escutando o TriAxis** (você aperta uma seta de PROGRAM no painel dele e ele avisa o número) ou pelo **teste de envio**.
  - **Tela inicial** com três caminhos:
    - **Meus timbres:** o painel do TriAxis, mais os 90 timbres com **nomes** dados pelo usuário;
    - **Backup:** guardar a memória ou devolver um backup;
    - **Tocar:** pedaleira na tela, em que cada pedal mostra o número do programa.
  - **Mandar para o TriAxis** em 3 passos: aviso, **backup de segurança obrigatório** e "Sim, substituir". Depois, o programa pergunta se o display piscou.
- **Modo avançado:** as abas descritas abaixo (Conexão, Controle, Backup, Restaurar, Biblioteca, Editor, Visualizar, Configurações, Log), para quem usa MIDI de verdade (ex.: RME + FCB1010).

As proteções são as mesmas nos dois modos. O modo avançado mantém a confirmação digitada (`RESTAURAR`).

> ## ⚠ Aviso: risco de sobrescrever a memória do TriAxis
> Restaurar um backup **substitui toda a memória atual do TriAxis**: os 90 presets, o mapa de programas MIDI e as configurações de FX loop/switching. Não dá para desfazer pelo equipamento.
> Por isso o programa:
> - exige **confirmação em duas etapas**: marcar a caixa e digitar `RESTAURAR`;
> - **oferece um backup de segurança** do estado atual antes de restaurar;
> - **bloqueia** arquivos vazios, sem `F0` no início, sem `F7` no fim ou com bytes inválidos;
> - **não envia nada ao TriAxis ao abrir**: listar portas e testar a conexão não transmite bytes.
>
> Mesmo assim, guarde cópias dos seus `.syx` fora do computador (pendrive ou nuvem).
>
> **Dica do manual do TriAxis:** os presets de fábrica 1–20 são carregados de novo toda vez que o aparelho é ligado, a não ser que você desative isso no painel (SHIFT uma vez, depois LOOP).

---

## Sumário
1. [Requisitos do Windows](#requisitos-do-windows)
2. [Instalar a interface MIDI](#instalar-a-interface-midi)
3. [Ligação: TriAxis, FCB1010 e G-Major](#ligação-triaxis-fcb1010-e-g-major)
4. [Primeiro backup](#primeiro-backup)
5. [Restaurar](#restaurar)
6. [Biblioteca, comparação e visualização](#biblioteca-comparação-e-visualização)
7. [Editor visual](#editor-visual)
8. [Modo controle (pedaleira)](#modo-controle-pedaleira)
9. [Solução de problemas MIDI](#solução-de-problemas-midi)
10. [Compilar e publicar](#compilar-e-publicar)
11. [Estrutura do projeto](#estrutura-do-projeto)
12. [Decisões técnicas](#decisões-técnicas)
13. [Referências e créditos](#referências-e-créditos)

---

## Requisitos do Windows

| Item | Requisito |
|---|---|
| Sistema | Windows 10 ou 11, 64 bits (x64) |
| Para **usar** | Apenas o `TriAxisBackupEditor.exe` publicado. Ele é autônomo e não precisa de .NET instalado. |
| Para **compilar** | [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.x) |
| Hardware | Interface MIDI USB com MIDI IN e MIDI OUT, mais 2 cabos MIDI DIN de 5 pinos |
| Rede | Nenhuma. O programa não usa internet e não tem telemetria. |

Onde o programa guarda os dados (tudo local):

| O quê | Onde |
|---|---|
| Backups `.syx` (padrão) | `Documentos\TriAxis Backups\` (dá para trocar a pasta) |
| Recepções incompletas | `...\TriAxis Backups\Incompletos\*.syx.part` |
| Biblioteca (SQLite) | `%LOCALAPPDATA%\TriAxisBackupEditor\biblioteca.db` |
| Configurações | `%LOCALAPPDATA%\TriAxisBackupEditor\configuracoes.json` |
| Logs técnicos | `%LOCALAPPDATA%\TriAxisBackupEditor\logs\triaxis-AAAAMMDD.log` |

## Instalar a interface MIDI

1. Conecte a interface USB. Interfaces *class compliant* (Roland UM-ONE, M-Audio Uno, ESI, iConnectivity etc.) são instaladas sozinhas pelo Windows.
2. Se o fabricante oferecer um driver próprio (por exemplo, o modo "advanced driver" da UM-ONE), prefira-o: costuma lidar melhor com SysEx longo.
3. Abra o programa, vá em **Conexão** e confira se a interface aparece em **MIDI IN** e **MIDI OUT**. A lista se atualiza sozinha a cada 3 segundos.
4. **Evite cabos USB-MIDI genéricos muito baratos.** Muitos perdem ou corrompem SysEx longo. O dump do TriAxis tem cerca de 1,1 KB (1.126 bytes) numa única mensagem.
5. Feche outros programas que usem MIDI (DAW, MIDI-OX, editores de G-Major/pedaleira). O driver padrão do Windows não deixa dois programas abrirem a mesma porta.

## Ligação: TriAxis, FCB1010 e G-Major

Para backup e restauração, ligue o TriAxis **direto** na interface:

```
TriAxis MIDI OUT   ──►  MIDI IN da interface     (backup: o TriAxis envia o dump)
Interface MIDI OUT ──►  TriAxis MIDI IN          (restauração: o PC envia o dump)
```

Num rig típico com **Behringer FCB1010** e **TC Electronic G-Major**:

```
Uso normal (show/ensaio):
FCB1010 MIDI OUT ──► TriAxis MIDI IN
TriAxis MIDI OUT/THRU ──► G-Major MIDI IN
```

- **Durante backup/restauração**, desconecte o cabo que vem da FCB1010 e ligue no TriAxis MIDI IN o cabo do MIDI OUT da interface. Assim nenhuma mensagem da pedaleira se mistura ao dump.
- Desconecte também o G-Major da saída do TriAxis durante a **restauração**, se ela estiver em modo THRU. Ele deve ignorar SysEx de outro fabricante, mas é mais seguro não deixar o dump passar adiante.
- Depois, volte os cabos para o arranjo normal.
- Os nomes no painel traseiro (OUT ou OUT/THRU) variam entre revisões do TriAxis. Confira no seu equipamento.

## Primeiro backup

1. **Conexão:** selecione **MIDI IN** e **MIDI OUT** e clique em **Testar conexão**. Ele abre as portas e escuta a entrada por 5 s, sem enviar nada. Mudar de preset no TriAxis durante o teste ajuda a confirmar que a entrada está viva.
2. **Backup:** confira a pasta de destino e, se quiser, escreva uma observação (ex.: "timbres do show de sábado").
3. Clique em **Fazer backup**. A tela mostra em destaque **"Pressione SHIFT + PRESET DOWN no painel do TriAxis"**.
4. Pressione **SHIFT + PRESET DOWN** no TriAxis. A barra avança e o número de bytes recebidos sobe.
5. Ao chegar o `F7` final, e após 1,5 s de silêncio, o programa:
   - grava `TriAxis_Backup_AAAA-MM-DD_HH-mm-ss.syx` **exatamente como recebido**;
   - relê o arquivo do disco e confere o **SHA-256**;
   - registra na biblioteca o nome, a data, o tamanho, o hash, a observação e o cabeçalho.
6. Se o tamanho for diferente de **1.126 bytes**, o tamanho do dump de um TriAxis real, o arquivo é salvo mesmo assim e o programa avisa. Essa informação é preciosa para validar o mapa: anote o tamanho e veja [docs/SYSEX.md](docs/SYSEX.md).

Se a recepção for interrompida (timeout, cancelamento ou cabo solto), o que chegou é salvo **apenas** como `Incompletos\..._INCOMPLETO.syx.part`. Esse arquivo não entra na biblioteca e não pode ser restaurado.

**Regras de segurança:** o backup só começa com MIDI IN e MIDI OUT selecionados. O programa espera o dump por até 60 s e aceita no máximo 10 s sem bytes no meio da recepção (os dois tempos são configuráveis). Você pode cancelar a qualquer momento.

## Restaurar

1. **Restaurar → Selecionar .syx**, ou **Biblioteca → Restaurar**. O arquivo é validado na hora. Arquivo vazio, sem `F0`/`F7` ou com bytes de status no meio **bloqueia** o botão.
2. Ajuste o envio em blocos, se necessário. O padrão é **128 bytes** com **20 ms** de intervalo (ajustável de 5 a 50 ms). Interfaces antigas podem precisar de intervalos maiores.
3. Clique em **Restaurar backup...** e uma janela vermelha de confirmação aparece:
   - **Backup de segurança:** salva o estado atual do TriAxis antes de sobrescrever (recomendado).
   - **Etapa 1:** marque "Entendo que a restauração substituirá a memória atual do TriAxis".
   - **Etapa 2:** digite `RESTAURAR`.
   - Se o arquivo tiver avisos (cabeçalho ou tamanho fora do mapa), também é preciso reconhecê-los.
4. **Enviar ao TriAxis.** Antes do envio, o arquivo é relido e o hash conferido: se ele mudou desde a validação, a restauração é bloqueada.
5. Ao terminar, **confira se o display do TriAxis piscou**, o que confirma o recebimento. Depois teste alguns presets.

O cancelamento vale **antes do envio e entre mensagens**. Uma mensagem SysEx iniciada é sempre enviada até o `F7`, para nunca deixar dados parciais no TriAxis. Não é possível fechar o programa no meio de uma restauração.

## Biblioteca, comparação e visualização

- **Biblioteca:** mostra nome, data, tamanho, SHA-256 (o hash completo aparece no tooltip), origem, observação e se o arquivo ainda existe. Permite:
  - buscar por nome, data (`2026-10-16` ou `16/10/2026`), observação ou hash;
  - importar `.syx` existentes;
  - abrir a pasta, visualizar, restaurar, duplicar, renomear e editar a observação;
  - excluir: o arquivo vai para a Lixeira do Windows, ou você pode remover só da biblioteca;
  - exportar CSV (separado por `;`, abre direto no Excel).
- **Comparar 2 selecionados** (Ctrl+clique): compara os hashes e lista as diferenças byte a byte. Quando os dois seguem o mapa preliminar, também mostra a região e os presets afetados.
- **Visualizar dump** mostra:
  - tamanho, SHA-256 e cabeçalho;
  - validação;
  - hex dump com coluna ASCII, em fonte monoespaçada;
  - quando o arquivo bate com o mapa preliminar, a tabela dos **90 presets** e o mapa dos **128 programas MIDI**, somente leitura. O botão **Abrir no editor** leva o arquivo para a aba Editor.

## Editor visual

A aba **Editor** recria a frente do TriAxis, desenhada do zero em vetor (inspirada no triaxiseditor.com):

| Elemento | Como usar |
|---|---|
| **PRESET** (display vermelho, ◄ ►) | Escolhe o preset em edição (1–90). A roda do mouse sobre o display também muda. |
| **PROGRAM / CHANNEL** (display verde, ◄ ►) | Navega pelos Program Change 1–128 e mostra o preset associado (só quando o mapa de programas do backup parece válido). |
| **PGM/CH** | Ligado (LED vermelho): as setas de PRESET escolhem **qual preset o Program Change atual chama**. Fica desativado enquanto o mapa de programas não for confirmado. |
| **GAIN … DYNAMIC VOICE** | ◄ ►, roda do mouse, ou clique no display para escolher numa lista (0.0 … 10). Um ponto âmbar marca o que mudou. |
| **Matriz de LEDs RHY / LD1 / LD2** | Clique no LED do modo (Rhythm G/Y, Lead 1 G/Y/R, Lead 2 G/Y/R). |
| **SW 1–4 e FX LOOP** | Clique para ligar/desligar. Os nomes dados em *Configurações → Saídas e loops* (ex.: funções da Simul-Class 2:90) aparecem nas dicas dos LEDs e no cartão do preset. |
| **MODE / LOOP SW** | MODE passa para o próximo modo; LOOP SW liga/desliga o FX LOOP. |
| **ENTR / EXIT** | ENTR = Salvar como novo .syx; EXIT = desfazer o preset atual (como "gravar" e "sair sem gravar" no aparelho). |
| **SHIFT, OUTPUT** | Só decorativos (o volume OUTPUT não faz parte dos presets). |

Abaixo do painel:
- **Copiar/Colar preset** e **Desfazer este preset/Desfazer tudo**;
- a lista das **alterações** (ex.: `Preset 03 · Gain: 5.0 → 7.0`);
- a **matriz dos 90 presets** (clique numa linha para editá-la).

**Regras de segurança do editor:**
- trabalha sobre uma **cópia em memória**: o `.syx` de origem nunca é alterado;
- **Salvar como novo .syx** grava `TriAxis_Editado_AAAA-MM-DD_HH-mm-ss.syx` na biblioteca (origem "Editado");
- só bytes de presets (5–994) mudam. Cabeçalho, região final (995–1124, onde a referência diz haver um mapa de programas, ainda não confirmado), `F7` e bits desconhecidos são preservados bit a bit, e há testes automáticos para isso;
- **Enviar ao TriAxis...** salva (se preciso) e abre a aba **Restaurar**, com a confirmação em duas etapas. Arquivos do editor exigem também reconhecer o aviso de "mapa preliminar";
- antes de confiar no editor, faça o teste de ida e volta do [plano de testes](docs/PLANO_DE_TESTES.md) (seção D) e os testes do editor (seção F).

## Modo controle (pedaleira)

A aba **Controle** transforma o programa num controlador MIDI do TriAxis. O modo envia **apenas Program Change e Control Change**. **Nada é gravado na memória do TriAxis.**

Segundo o manual oficial do TriAxis:
- **Program Change é o jeito de trocar de timbre.** O aparelho tem 128 *programas* (posições MIDI) que apontam para os 90 *presets*; cada preset guarda o modo (canal Rhythm/Lead), o FX Loop e os switches 1–4.
- **Control Change não troca canal, FX Loop nem switches.** Só move os nove botões de timbre (Gain, Treble...), num número de controle que você atribui a cada preset no painel (SHIFT três vezes).
- O **MIDI OUT** do aparelho manda um Program Change quando você usa as teclas de PROGRAM do painel. É assim que o assistente consegue *escutar* o canal.

1. Em **Conexão**, selecione o **MIDI OUT** ligado ao **MIDI IN do TriAxis**.
2. Em **Controle**, escolha:
   - o **canal MIDI** em que o TriAxis recebe (o mesmo configurado no equipamento);
   - os **pedais por banco** (10, como a FCB1010; ou 4, 5 ou 8).
3. Clique em **Ativar modo controle**. Enquanto ele está ativo, backup e restauração ficam bloqueados, porque a porta MIDI está em uso.

| Controle | Ação |
|---|---|
| **Pedais 1–10** (clique) | Program Change do banco atual. Com 10 por banco: banco 0 → programas 1–10, banco 1 → 11–20, …, banco 12 → 121–128. O LED acende no pedal ativo. |
| **BANCO ▲ ▼** | Muda o banco (a FCB1010 também não envia nada ao trocar de banco: só o pedal envia). |
| **PROGRAMA −1 / +1** | Programa anterior/seguinte. |
| **Botões CC 1–4** | Alternam CC = 127/0 no canal. Nome e número do CC são editáveis (padrão CC 80–83). **Atenção:** segundo o manual do TriAxis, ele não troca de canal, FX Loop nem switches por CC (isso é feito pelos programas, ou seja, Program Change). O CC só move os nove botões de timbre, e só no número de controle que você atribuiu a cada um no painel. Os botões servem para pedais de expressão ou para outros aparelhos. |

**Teclado** (com a aba aberta): `1`…`9`, `0` = pedais 1–10 (o teclado numérico também funciona); `↑`/`PgUp` = banco +; `↓`/`PgDn` = banco −; `→`/`←` = programa ±1.

**Entradas MIDI (aprender):** se você tiver uma pedaleira ou controlador ligado ao PC:
1. escolha a **Entrada de controle**;
2. ative o modo;
3. clique em **Aprender** ao lado da ação (pedal 1–10, banco ▲▼, programa ±1, botões CC);
4. pise no pedal.

Program Change, CC (dispara ao pressionar, ou seja, valor > 0) e notas podem ser aprendidos. A opção **Repassar Program Change da entrada** reenvia ao TriAxis, no canal configurado, os PCs que não tiverem ação associada. Tudo é salvo nas configurações.

## Solução de problemas MIDI

| Sintoma | O que fazer |
|---|---|
| Nenhuma interface na lista | Reconecte o USB, troque de porta USB e aguarde o Windows instalar o driver. Clique em **Atualizar dispositivos**. |
| "A porta já está em uso por outro programa" | Feche DAW, MIDI-OX, editores de G-Major/FCB e outras instâncias deste programa. |
| "Nenhum dado recebido" no backup | Confira se o cabo vai do **MIDI OUT do TriAxis ao MIDI IN da interface**. Confira se a porta MIDI IN selecionada é a da interface. Pressione SHIFT + PRESET DOWN dentro do prazo. |
| Backup incompleto (sem F7) | Use um cabo/interface melhor, evite hubs USB, aumente o **timeout de recepção** em Configurações e repita. |
| Backup com tamanho diferente de 1.126 bytes | O arquivo foi salvo sem alterações. Pode ser uma versão diferente de firmware/mapa. Faça 2 ou 3 backups seguidos e compare os hashes: se forem idênticos, a captura é confiável. |
| O TriAxis não troca de programa | No assistente (passo 4), clique em **Escutar o TriAxis** e aperte uma seta de PROGRAM no painel dele. Se não ouvir nada, confira o cabo; se ouvir, ele mostra o canal. |
| Meus timbres 1–20 voltam aos de fábrica ao ligar | O TriAxis recarrega os presets de fábrica 1–20 ao ser ligado. Aperte SHIFT e depois LOOP no painel para desativar. |
| Display do TriAxis não pisca na restauração | Confira se o cabo vai do **MIDI OUT da interface ao MIDI IN do TriAxis**. Aumente o intervalo entre blocos (ex.: 40–50 ms) e/ou reduza o bloco para 64 bytes. Retire a FCB1010 da cadeia. |
| Interface desconectada no meio | O programa avisa e não trava. Reconecte, selecione as portas de novo e repita a operação inteira. |
| Quero ver o que aconteceu | Aba **Log**, ou o arquivo em `%LOCALAPPDATA%\TriAxisBackupEditor\logs`. Ele registra dispositivos, bytes recebidos/enviados, hashes e erros. |

## Compilar e publicar

Pré-requisito: **.NET 8 SDK** instalado (`dotnet --list-sdks` deve mostrar `8.0.x`).

```bash
dotnet restore TriAxisBackupEditor.sln
```

```bash
dotnet build TriAxisBackupEditor.sln -c Release
```

```bash
dotnet test TriAxisBackupEditor.sln -c Release
```

Executável **autônomo para Windows x64, em arquivo único** (não exige .NET na máquina de destino):

```bash
dotnet publish src/TriAxisBackupEditor.App/TriAxisBackupEditor.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish/win-x64
```

Resultado: `publish/win-x64/TriAxisBackupEditor.exe`, com cerca de 65 MB e tudo embutido, incluindo o SQLite nativo.

O script `publish.ps1` faz restore, roda os testes (e cancela se algum falhar), publica e mostra o SHA-256 do `.exe`:

```bash
powershell -ExecutionPolicy Bypass -File publish.ps1
```

Versão menor, que depende do *.NET 8 Desktop Runtime* instalado:

```bash
dotnet publish src/TriAxisBackupEditor.App/TriAxisBackupEditor.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/win-x64-fdd
```

## Estrutura do projeto

```
TriAxisBackupEditor/
├─ src/
│  ├─ TriAxisBackupEditor.App/          WPF (MVVM): Views, ViewModels, painel 7 segmentos, pedaleira, tema escuro
│  ├─ TriAxisBackupEditor.Application/  Casos de uso: captura, biblioteca, restauração, modo controle, log
│  ├─ TriAxisBackupEditor.Midi/         IMidiDeviceService + implementação WinMM (winmm.dll)
│  ├─ TriAxisBackupEditor.SysEx/        Validação, remontagem, hash, hex, mapa preliminar, editor de dump
│  ├─ TriAxisBackupEditor.Storage/      SQLite (IBackupRepository), arquivos, configurações, CSV
│  └─ TriAxisBackupEditor.Tests/        testes xUnit
├─ docs/
│  ├─ manual/             Manual do usuário (português e inglês)
│  ├─ SYSEX.md            Formato, mapa preliminar, plano de validação de offsets
│  ├─ ROADMAP.md          Fases 1–7
│  └─ PLANO_DE_TESTES.md  Roteiro de testes com o TriAxis real
├─ publish.ps1
├─ README.md / README.en.md
└─ TriAxisBackupEditor.sln
```

Interfaces principais:

| Interface | Projeto | Papel |
|---|---|---|
| `IMidiDeviceService` | Midi | Lista portas e abre IN/OUT (abrir nunca envia bytes) |
| `TriAxisDumpEditor` | SysEx | Edita uma cópia do dump, só nas regiões mapeadas |
| `ControlSurfaceService` | Application | Modo controle: Program Change/CC e entrada de controle |
| `TriAxisListener` | Application | Escuta o MIDI OUT do TriAxis até um Program Change (descobre o canal) |
| `ITriAxisSysExService` | SysEx | Valida, interpreta (mapa preliminar), compara, gera hash e hex |
| `IBackupRepository` | Storage | Metadados no SQLite |

Os testes cobrem:
- validação SysEx (vazio, sem F0/F7, status no meio, mensagens múltiplas);
- SHA-256 (vetor FIPS e hash de arquivo);
- nome automático de backup e nomes únicos;
- fragmentação e remontagem (blocos de 1 a 4096 bytes e aleatórios; bytes de tempo real intercalados);
- **bloqueio de restauração sem confirmação** (sem confirmação, só etapa 1, só etapa 2, palavra errada, avisos não reconhecidos, arquivo inválido, arquivo alterado após a validação);
- envio em blocos byte a byte idêntico;
- captura com timeout, cancelamento e dados parciais;
- SQLite, biblioteca, CSV e parser;
- **editor de dump** (só regiões editáveis mudam, original intacto, bits desconhecidos preservados, 500 edições aleatórias continuam válidas);
- o **formato real do dump (1.126 bytes)** e os arquivos do editor web (dump + zeros);
- **modo controle** (codificação de Program Change/CC, bancos estilo FCB1010, MIDI learn, nunca envia SysEx) e o **ouvinte do TriAxis**;
- **modo simples**: pareamento automático de entrada/saída pelo nome, envio seguro que exige backup de segurança antes de liberar a substituição, nomes dos timbres nos pedais;
- enumeração real das portas WinMM.

## Decisões técnicas

- **MIDI via WinMM nativo, em vez de DryWetMidi.** A especificação pedia DryWetMidi "ou outra biblioteca madura". Optei pela própria API MIDI do Windows (`winmm.dll`, presente do Windows 95 ao 11) por dois motivos:
  1. **Recepção:** é preciso controlar os buffers de SysEx. Aqui são 16 buffers de 4 KB, e o `SysExAssembler` remonta qualquer fragmentação.
  2. **Envio:** é preciso mandar **trechos crus** de um SysEx em blocos com intervalo (`midiOutLongMsg` por bloco, esperando `MHDR_DONE` antes de liberar a memória). Bibliotecas de alto nível enviam o evento inteiro de uma vez.

  Tudo fica atrás de `IMidiDeviceService`. Trocar por outra biblioteca é uma nova implementação, sem mexer no resto.
- **Nada é alterado no dump.** A captura guarda as mensagens `F0…F7` na ordem de chegada. A única filtragem é a exigida pela especificação MIDI: bytes de tempo real (`F8`–`FF`) intercalados não fazem parte do SysEx e são descartados. A contagem aparece no log e num aviso.
- **Nunca sobrescrever.** Arquivos novos usam `FileMode.CreateNew`. Nomes repetidos ganham `_2`, `_3`. Renomear não substitui. Excluir manda para a Lixeira.
- **Sem requisição de dump por padrão.** Não há comando de *dump request* documentado para o TriAxis. O aparelho manda o dump quando se aperta SHIFT + PRESET DOWN. Existe uma opção avançada para enviar uma mensagem informada pelo usuário, desligada e com aviso.

## Referências e créditos

- **markphilpot/triaxiseditor**, editor web publicado em triaxiseditor.com ([GitHub](https://github.com/markphilpot/triaxiseditor)), licença GPL-3.0. É uma página HTML/JS que abre e grava `.syx`, sem comunicação MIDI. Dele vêm o **mapa preliminar** dos presets (90 × 11 bytes, 128 programas) e as tabelas de valores. A referência supunha um dump de 2.746 bytes; um TriAxis real manda 1.126 (veja [docs/SYSEX.md](docs/SYSEX.md)), e os arquivos do editor web são esse dump seguido de zeros, que este programa também abre. O código deste projeto foi escrito do zero em C#: só os fatos do formato foram usados, com crédito e marcados como **não validados em hardware**. Se no futuro algum trecho de código daquele projeto for portado, a GPL-3.0 passará a valer para este código.
- Mesa/Boogie, TriAxis e Simul-Class são marcas de seus respectivos donos. Este é um projeto independente, sem relação com o fabricante. O painel do editor é um desenho vetorial próprio; nenhuma foto ou logotipo do fabricante está embutido.
- Desenvolvido por **@palinetes**.
