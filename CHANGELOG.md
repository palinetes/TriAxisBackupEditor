# Changelog

## v1.0.0 (2026-10-21)

Primeira versão **estável** / First **stable** release.

### Português
Backup, restauração, edição e controle MIDI do Mesa/Boogie TriAxis no Windows 10/11, sem precisar entender de MIDI. Testada com um TriAxis real e uma Simul-Class 2:90.

- **Modo simples** (assistente de ligação, Meus timbres, Backup, Tocar) e **modo avançado** (biblioteca, editor completo, hex, log, aprender MIDI).
- **Segurança:** o programa nunca muda nada no TriAxis sem você pedir, guarda um backup de segurança antes de qualquer envio e confere cada arquivo antes de mandar.
- **Escutar o TriAxis:** descobre o número (canal) do aparelho quando você aperta uma seta de PROGRAM no painel dele.
- **Controle:** pedaleira na tela com bancos e teclado. O TriAxis troca de timbre por Program Change; o CC só move os nove botões de timbre.
- **Simul-Class 2:90:** um botão preenche os nomes das saídas SW1 Deep, SW2 Modern, SW3 1/2 Drive.
- **Português e inglês**, com manual (F1) nos dois idiomas. Tudo fica no seu computador: sem internet e sem telemetria.
- Como usar: baixe `TriAxisBackupEditor.exe` (em *Assets*), abra e siga o assistente. O Windows pode avisar "editor desconhecido" porque o programa não tem assinatura digital: clique em **Mais informações → Executar assim mesmo**.

### English
Backup, restore, editing and MIDI control for the Mesa/Boogie TriAxis on Windows 10/11, with no MIDI knowledge needed. Tested with a real TriAxis and a Simul-Class 2:90.

- **Simple mode** (setup wizard, My sounds, Backup, Play) and **advanced mode** (library, full editor, hex, log, MIDI learn).
- **Safety:** the program never changes anything on the TriAxis unless you ask, saves a safety backup before any send, and checks every file before sending.
- **Listen to the TriAxis:** finds the unit's number (channel) when you press a PROGRAM arrow on its panel.
- **Control:** on-screen pedalboard with banks and keyboard. The TriAxis changes sound by Program Change; CC only moves the nine sound knobs.
- **Simul-Class 2:90:** a button fills in the output names SW1 Deep, SW2 Modern, SW3 1/2 Drive.
- **Portuguese and English**, with the manual (F1) in both languages. Everything stays on your computer: no internet, no telemetry.
- How to use: download `TriAxisBackupEditor.exe` (under *Assets*), open it and follow the wizard. Windows may warn about an "unknown publisher" because the program is not digitally signed: click **More info → Run anyway**.

Por **@palinetes** · Licença MIT / MIT License

## v1.0.0-beta.7 (2026-10-21)

### Português
- **Corrigido: o manual (F1 e botão Manual) não abria em inglês.** O arquivo `index.en.html` não entrava no programa (o .NET o tratava como recurso de idioma). Agora entra, e se a versão no idioma da tela faltar, o programa abre a versão em português.

### English
- **Fixed: the manual (F1 and the Manual button) did not open in English.** The `index.en.html` file was not being included in the program (.NET treated it as a language resource). It is now included, and if the version in the screen language is missing, the program opens the Portuguese one.

Por **@palinetes** · Licença MIT / MIT License

## v1.0.0-beta.6 (2026-10-20)

### Português
- O botão de nomes da **Simul-Class 2:90** agora segue a ordem dos conectores na traseira do amp: **SW1 Deep, SW2 Modern, SW3 1/2 Drive**. Os nomes continuam editáveis (SW4 e FX LOOP ficam livres).

### English
- The **Simul-Class 2:90** names button now follows the order of the jacks on the amp's rear panel: **SW1 Deep, SW2 Modern, SW3 1/2 Drive**. The names stay editable (SW4 and FX LOOP are left free).

Por **@palinetes** · Licença MIT / MIT License

## v1.0.0-beta.5 (2026-10-20)

### Português
- **Simul-Class 2:90:** em *Avançado → Configurações → Saídas e loops*, um botão preenche os nomes das saídas com as três entradas de voicing da 2:90 (SW1 Deep, SW2 Modern, SW3 1/2 Drive, na ordem dos conectores da traseira da 2:90). Confira se seus cabos estão nessa ordem. O manual (seção 10) explica o que cada voicing faz.

### English
- **Simul-Class 2:90:** in *Advanced → Settings → Outputs and loops*, a button fills in the output names with the three voicing inputs of the 2:90 (SW1 Deep, SW2 Modern, SW3 1/2 Drive, in the order of the jacks on the 2:90 rear panel). Check that your cables are in that order. The manual (section 10) explains what each voicing does.

Por **@palinetes** · Licença MIT / MIT License

## v1.0.0-beta.4 (2026-10-20)

**Português · English** — o programa agora é bilíngue / the program is now bilingual.

### Português
- **Idioma:** o programa fala **português e inglês**. Na primeira vez ele usa o idioma do Windows; dá para trocar a qualquer momento pela chave **PT | EN** no topo da janela (o programa reinicia para aplicar). Todas as telas, mensagens, avisos e o manual (F1) existem nos dois idiomas. A palavra que se digita para liberar o envio no modo avançado acompanha o idioma (`RESTAURAR` / `RESTORE`).
- **Documentação em inglês:** `README.en.md` e `docs/manual/index.en.html`.
- **Imagens do manual refeitas** com o programa atual (o passo 4 do assistente já mostra o novo botão *Escutar o TriAxis*).
- O log técnico continua em português (é para suporte).

### English
- **Language:** the program speaks **Portuguese and English**. On first run it uses the Windows language; you can switch at any time with the **PT | EN** switch at the top of the window (the program restarts to apply it). Every screen, message, warning and the manual (F1) exist in both languages. The word typed to enable sending in advanced mode follows the language (`RESTAURAR` / `RESTORE`).
- **English documentation:** `README.en.md` and `docs/manual/index.en.html`.
- **Manual screenshots redone** with the current program (wizard step 4 now shows the new *Listen to the TriAxis* button).
- The technical log stays in Portuguese (it is meant for support).

Por **@palinetes** · Licença MIT / MIT License

## v1.0.0-beta.3 (2026-10-20)

Correções no **controle** e na descoberta do número (canal) do TriAxis, a partir do manual oficial.

### O que mudou
- **Escutar o TriAxis** (passo 4 do assistente): clique no botão e aperte uma seta de PROGRAM no painel do TriAxis. Segundo o manual, o MIDI OUT dele manda uma troca de programa nesse momento, então o programa descobre o **número (canal) certo em um passo**, sem tentativa e erro. Só lê, não envia nada.
- **Botões CC**: o manual confirma que o TriAxis **não troca de canal (Rhythm/Lead), FX Loop nem switches por CC**. Isso é feito pelos programas (Program Change), porque cada timbre guarda o modo, o loop e os switches. O CC só move os nove botões de timbre, no número de controle atribuído no painel. A tela agora explica isso.
- **Aviso sobre os timbres 1–20**: o TriAxis recarrega os presets de fábrica 1–20 toda vez que é ligado, a não ser que a recarga seja desativada (SHIFT, depois LOOP). O envio e o manual agora avisam.
- Conferido com o manual oficial: a ordem dos nove parâmetros, o modo e o bit de SW1 batem com os presets de fábrica gravados no seu aparelho (veja `docs/SYSEX.md`).

### Atenção (beta)
- Encontrou um problema? Abra uma **issue** contando o que fez, o que esperava e o que aconteceu. Se puder, anexe o log de `%LOCALAPPDATA%\TriAxisBackupEditor\logs`.

Por **@palinetes** · Licença MIT

## v1.0.0-beta.2 (2026-10-17)

Correção: **backups não abriam**.

### O que mudou
- O programa agora usa o tamanho real do dump do TriAxis: **1.126 bytes**, confirmado num aparelho real (3 backups seguidos, todos idênticos). A versão anterior esperava 2.746 bytes, tamanho que vinha do editor web de referência, e por isso recusava backups verdadeiros no editor, no modo simples e na visualização.
- **Arquivos salvos no site triaxiseditor.com** (2.746 bytes: o dump real seguido de zeros) agora abrem, podem ser comparados e enviados. O programa usa só os 1.126 bytes que o TriAxis entende e **nunca altera o arquivo original**. Ao importar, guarda uma cópia limpa ao lado.
- A região depois dos presets, que a referência chamava de "mapa de programas", não parece ser isso no aparelho real. Ela é **preservada byte a byte**, mas não é editada. Na pedaleira, os pedais mostram o **número do programa** até isso ser confirmado.

### Atenção (beta)
- O significado de cada byte dos presets ainda está sendo conferido em aparelhos reais. Guarde seus backups e confira o resultado no TriAxis.
- Encontrou um problema? Abra uma **issue** contando o que fez, o que esperava e o que aconteceu. Se puder, anexe o log de `%LOCALAPPDATA%\TriAxisBackupEditor\logs`.

Por **@palinetes** · Licença MIT

## v1.0.0-beta.1 (2026-10-17)

Primeira versão pública, **em teste**. Backup, restauração, edição e controle MIDI do Mesa/Boogie TriAxis no Windows 10/11, sem precisar entender de MIDI.

### Como usar
1. Baixe `TriAxisBackupEditor.exe` (em *Assets*, abaixo). É um arquivo único, sem instalação.
2. Abra o arquivo. Na primeira vez, o **assistente** mostra como ligar os cabos, faz o primeiro backup dos seus timbres e descobre sozinho o número (canal) do seu TriAxis.
3. A tecla **F1** abre o manual.

> O Windows pode avisar que o app é de um "editor desconhecido", porque ele não tem assinatura digital. Clique em **Mais informações → Executar assim mesmo**.

### O que tem
- **Modo simples:**
  - assistente de ligação;
  - **Meus timbres**: painel do TriAxis e nomes para os 90 timbres;
  - **Backup**: guardar a memória ou devolver um backup;
  - **Tocar**: pedaleira na tela, com bancos e teclado;
  - envio sempre com **backup de segurança automático**.
- **Modo avançado:** biblioteca com comparação byte a byte, editor completo, visualização em hexadecimal, log, botões CC e aprender MIDI (FCB1010 etc.).
- **"O TriAxis não troca de programa?":** um teste diz se o problema é o cabo ou o número e acerta o número em até 4 perguntas.
- Backup que está só esperando o TriAxis pode ser cancelado ou fechado a qualquer momento. Se nada chegar, as dicas aparecem em 15 segundos.
- Tudo fica no seu computador: sem internet e sem telemetria.

### Atenção (beta)
- Backup e restauração usam o formato do próprio TriAxis. A **edição** dos timbres usa um mapa da memória que ainda está sendo conferido em aparelhos reais. Guarde seus backups e confira o resultado no TriAxis.
- Encontrou um problema? Abra uma **issue** contando o que fez, o que esperava e o que aconteceu. Se puder, anexe o log de `%LOCALAPPDATA%\TriAxisBackupEditor\logs`.

Por **@palinetes** · Licença MIT
