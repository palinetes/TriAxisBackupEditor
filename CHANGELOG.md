# Changelog

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
