namespace TriAxisBackupEditor.Localization;

internal static partial class English
{
    /// <summary>Entradas escritas à mão: palavras soltas e textos usados dentro de outras frases.</summary>
    private static partial void AddMore(Dictionary<string, string> map)
    {
        // Rótulos ligados a valores (ex.: "Modo: " + valor)
        map["Modo:"] = "Mode:";
        map["Pasta de dados:"] = "Data folder:";
        map["Biblioteca:"] = "Library:";
        map["em andamento"] = "in progress";

        // Palavras usadas dentro de frases
        map["ligado"] = "on";
        map["desligado"] = "off";
        map["ATIVADA"] = "ON";
        map["desativada"] = "off";
        map["hoje"] = "today";
        map["ontem"] = "yesterday";
        map["nenhum"] = "none";

        // Formatos de data
        map["dd/MM/yyyy"] = "yyyy-MM-dd";
        map["dd/MM/yyyy 'às' HH:mm"] = "yyyy-MM-dd 'at' HH:mm";

        // Títulos e botões soltos
        map["Concluir"] = "Finish";
        map["Erro"] = "Error";
        map["Erro inesperado"] = "Unexpected error";
        map["\n\nOs detalhes foram registrados no log."] = "\n\nThe details were recorded in the log.";
        map["Importar"] = "Import";
        map["Renomear"] = "Rename";
        map["Excluir"] = "Delete";
        map["Cancelado"] = "Cancelled";
        map["Tocar"] = "Play";
        map["ERRO"] = "ERROR";
        map["AVISO"] = "WARNING";

        // A palavra que se digita para liberar o envio
        map["RESTAURAR"] = "RESTORE";

        // Nomes de fábrica dos botões CC e origens na biblioteca
        map["Botão 1"] = "Button 1";
        map["Botão 2"] = "Button 2";
        map["Botão 3"] = "Button 3";
        map["Botão 4"] = "Button 4";
        map["Capturado"] = "Captured";
        map["Importado"] = "Imported";
        map["Duplicado"] = "Duplicated";
        map["Editado"] = "Edited";

        // Texto-fonte do mapa
        map["tamanho e presets conferidos num TriAxis real; valores dos knobs vindos de markphilpot/triaxiseditor"] =
            "size and presets checked on a real TriAxis; knob values from markphilpot/triaxiseditor";
        map["Visualização somente leitura. Para editar, use a aba Editor (mapa preliminar, não validado em hardware; as edições geram um novo arquivo)."] =
            "Read-only view. To edit, use the Editor tab (preliminary map, not validated on hardware; edits produce a new file).";

        // Seletor de idioma (igual nos dois idiomas)
        map["PT"] = "PT";
        map["EN"] = "EN";
        map["Português"] = "Português";
        map["English"] = "English";

        // Textos que antes eram "Setter" de estilo e hoje vêm do ViewModel
        map["Os timbres na tela são iguais aos do último backup."] = "The sounds on the screen are the same as the last backup.";
        map["Você fez mudanças que ainda não foram para o TriAxis."] = "You made changes that have not gone to the TriAxis yet.";
        map["Começar"] = "Start";
        map["Aguardando..."] = "Waiting...";
        map["Aprender"] = "Learn";

        // Nomes das saídas para a Simul-Class 2:90
        map["2:90 Deep"] = "2:90 Deep";
        map["2:90 1/2 Drive"] = "2:90 1/2 Drive";
        map["2:90 Modern"] = "2:90 Modern";
        map["Nomes da 2:90 preenchidos (SW1 Deep, SW2 Modern, SW3 1/2 Drive). Se os seus cabos estiverem em outra ordem, ajuste os nomes. Clique em Salvar para aplicar."] =
            "2:90 names filled in (SW1 Deep, SW2 Modern, SW3 1/2 Drive). If your cables are in a different order, adjust the names. Click Save to apply.";
        map["Usar os nomes da Simul-Class 2:90 (Deep, 1/2 Drive, Modern)"] = "Use the Simul-Class 2:90 names (Deep, 1/2 Drive, Modern)";
        map["Na ordem dos conectores da 2:90 (Deep Extend, Modern, Half Drive): SW1 = Deep, SW2 = Modern, SW3 = 1/2 Drive. Confira se é assim que seus cabos estão ligados."] =
            "In the order of the 2:90 jacks (Deep Extend, Modern, Half Drive): SW1 = Deep, SW2 = Modern, SW3 = 1/2 Drive. Check that your cables are wired this way.";

        // Operações mostradas na barra de status
        map["Backup"] = "Backup";
        map["Restauração"] = "Restore";
        map["Teste de conexão"] = "Connection test";
        map["Modo controle"] = "Control mode";
    }
}
