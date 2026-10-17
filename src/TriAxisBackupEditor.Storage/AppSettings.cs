using System.Text.Json;

namespace TriAxisBackupEditor.Storage;

public sealed class AppSettings
{
    public const int MinChunkSize = 16;
    public const int MaxChunkSize = 4096;
    public const int MinChunkDelayMs = 5;
    public const int MaxChunkDelayMs = 50;

    public string BackupFolder { get; set; } = string.Empty;

    /// <summary>Tamanho dos blocos enviados na restauração (padrão 128 bytes).</summary>
    public int ChunkSize { get; set; } = 128;

    /// <summary>Intervalo entre blocos, 5–50 ms (padrão 20 ms).</summary>
    public int ChunkDelayMs { get; set; } = 20;

    /// <summary>Tempo para o usuário disparar o dump (SHIFT + PRESET DOWN) antes de desistir.</summary>
    public int StartTimeoutSeconds { get; set; } = 60;

    /// <summary>Tempo máximo sem receber bytes durante a recepção (padrão 10 s).</summary>
    public int ReceiveTimeoutSeconds { get; set; } = 10;

    /// <summary>Silêncio após um F7 para considerar o dump concluído (caso o equipamento envie mais de uma mensagem).</summary>
    public int PostDumpIdleMilliseconds { get; set; } = 1500;

    public bool LoggingEnabled { get; set; } = true;

    /// <summary>Requisição de dump: desligada por padrão, pois não há comando documentado para o TriAxis.</summary>
    public bool DumpRequestEnabled { get; set; }

    public string DumpRequestHex { get; set; } = string.Empty;

    public string? LastInputDeviceKey { get; set; }

    public string? LastOutputDeviceKey { get; set; }

    // ---------------- Modo controle ----------------

    /// <summary>Canal MIDI em que o TriAxis recebe (1–16).</summary>
    public int ControlMidiChannel { get; set; } = 1;

    /// <summary>Pedais por banco (4, 5, 8 ou 10; padrão 10 como a FCB1010).</summary>
    public int ControlBankSize { get; set; } = 10;

    /// <summary>Entrada MIDI opcional (pedaleira/controlador ligado ao PC) que dispara as ações.</summary>
    public string? ControlInputDeviceKey { get; set; }

    /// <summary>Repassa ao TriAxis os Program Change recebidos na entrada de controle que não estejam mapeados.</summary>
    public bool ControlForwardProgramChanges { get; set; }

    public List<ControlBindingSetting> ControlBindings { get; set; } = new();

    public List<CcButtonSetting> ControlCcButtons { get; set; } = new();

    /// <summary>
    /// Nomes opcionais para FX LOOP e SW1–SW4 (índices 0–4), conforme o que cada saída controla no rig
    /// (ex.: funções da Simul-Class 2:90). São só rótulos: não alteram o dump.
    /// </summary>
    public List<string> SwitchNames { get; set; } = new();

    // ---------------- Interface ----------------

    public const string SimpleMode = "Simple";
    public const string AdvancedMode = "Advanced";

    /// <summary>"Simple" (padrão, para quem não entende de MIDI) ou "Advanced".</summary>
    public string UiMode { get; set; } = SimpleMode;

    /// <summary>O assistente de ligação já foi concluído (ou pulado) pelo menos uma vez.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Nomes dos 90 timbres (índice 0 = preset 1). Só no computador: o TriAxis não guarda nomes.</summary>
    public List<string> PresetNames { get; set; } = new();

    public AppSettings Normalize(string defaultBackupFolder)
    {
        if (string.IsNullOrWhiteSpace(BackupFolder))
        {
            BackupFolder = defaultBackupFolder;
        }

        ChunkSize = Math.Clamp(ChunkSize, MinChunkSize, MaxChunkSize);
        ChunkDelayMs = Math.Clamp(ChunkDelayMs, MinChunkDelayMs, MaxChunkDelayMs);
        StartTimeoutSeconds = Math.Clamp(StartTimeoutSeconds, 5, 600);
        ReceiveTimeoutSeconds = Math.Clamp(ReceiveTimeoutSeconds, 1, 120);
        PostDumpIdleMilliseconds = Math.Clamp(PostDumpIdleMilliseconds, 200, 10_000);
        DumpRequestHex ??= string.Empty;
        ControlMidiChannel = Math.Clamp(ControlMidiChannel, 1, 16);
        if (ControlBankSize is not (4 or 5 or 8 or 10))
        {
            ControlBankSize = 10;
        }

        ControlBindings ??= new List<ControlBindingSetting>();
        if (UiMode is not (SimpleMode or AdvancedMode))
        {
            UiMode = SimpleMode;
        }

        PresetNames ??= new List<string>();
        while (PresetNames.Count < 90)
        {
            PresetNames.Add(string.Empty);
        }

        if (PresetNames.Count > 90)
        {
            PresetNames.RemoveRange(90, PresetNames.Count - 90);
        }

        for (var i = 0; i < PresetNames.Count; i++)
        {
            PresetNames[i] = (PresetNames[i] ?? string.Empty).Trim();
        }

        SwitchNames ??= new List<string>();
        while (SwitchNames.Count < 5)
        {
            SwitchNames.Add(string.Empty);
        }
        ControlCcButtons ??= new List<CcButtonSetting>();
        for (var i = ControlCcButtons.Count; i < 4; i++)
        {
            ControlCcButtons.Add(new CcButtonSetting { Label = $"Botão {i + 1}", Controller = 80 + i });
        }

        foreach (var button in ControlCcButtons)
        {
            button.Controller = Math.Clamp(button.Controller, 0, 127);
            button.Label ??= string.Empty;
        }

        return this;
    }

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this)) ?? new AppSettings();
}

/// <summary>Gatilho MIDI aprendido para uma ação do modo controle (persistido em JSON).</summary>
public sealed class ControlBindingSetting
{
    public string Action { get; set; } = string.Empty;

    /// <summary>ProgramChange, ControlChange ou Note.</summary>
    public string Kind { get; set; } = string.Empty;

    public int Channel { get; set; }

    public int Number { get; set; }
}

/// <summary>Botão de Control Change configurável (liga = 127, desliga = 0).</summary>
public sealed class CcButtonSetting
{
    public string Label { get; set; } = string.Empty;

    public int Controller { get; set; }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly AppPaths _paths;

    public SettingsStore(AppPaths paths) => _paths = paths;

    /// <summary>Último erro de leitura (arquivo corrompido etc.). As configurações padrão são usadas nesse caso.</summary>
    public string? LastLoadError { get; private set; }

    public AppSettings Load()
    {
        LastLoadError = null;
        try
        {
            if (File.Exists(_paths.SettingsPath))
            {
                var json = File.ReadAllText(_paths.SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null)
                {
                    return loaded.Normalize(_paths.DefaultBackupFolder);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            LastLoadError = $"Não foi possível ler as configurações ({ex.Message}). Usando valores padrão.";
        }

        return new AppSettings().Normalize(_paths.DefaultBackupFolder);
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_paths.DataFolder);
        var temp = _paths.SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _paths.SettingsPath, overwrite: true);
    }
}
