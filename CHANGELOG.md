# Changelog

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
