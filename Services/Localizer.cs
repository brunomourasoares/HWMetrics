using System.Globalization;

namespace WaterCoolerApp.Services;

/// <summary>
/// Textos da interface em 6 idiomas. A chave é sempre o texto em inglês (o mesmo que está no XAML),
/// e cada linha traz as traduções na ordem: pt, es, it, fr, zh. Texto sem entrada volta em inglês.
/// Para adicionar um idioma: inclua em <see cref="Languages"/> e uma coluna em cada linha de <see cref="Strings"/>.
/// </summary>
public static class Localizer
{
    public sealed record LanguageOption(string Code, string NativeName);

    /// <summary>Ordem = índice do ComboBox de idiomas e valor salvo em preferences.json (0 e 1 continuam en/pt).</summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English"),
        new("pt", "Português"),
        new("es", "Español"),
        new("it", "Italiano"),
        new("fr", "Français"),
        new("zh", "简体中文")
    ];

    public static string Current { get; private set; } = "en";

    public static IReadOnlyDictionary<string, string[]> Table => Strings;

    public static void SetLanguage(string code) => Current = IndexOf(code) >= 0 ? code : "en";

    public static int IndexOf(string code)
    {
        for (var i = 0; i < Languages.Count; i++)
            if (string.Equals(Languages[i].Code, code, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    public static string CodeAt(int index) => Languages[Math.Clamp(index, 0, Languages.Count - 1)].Code;

    /// <summary>Idioma do Windows, ou inglês se não houver tradução.</summary>
    public static int DetectSystemLanguageIndex()
    {
        var index = IndexOf(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        return index >= 0 ? index : 0;
    }

    public static string Get(string english) => Get(english, Current);

    public static string Get(string english, string code)
    {
        var index = IndexOf(code);
        if (index <= 0 || string.IsNullOrEmpty(english)) return english;
        return Strings.TryGetValue(english, out var row) ? row[index - 1] : english;
    }

    /// <summary>O texto em inglês e todas as traduções (usado para achar a janela já aberta em qualquer idioma).</summary>
    public static IEnumerable<string> AllVariants(string english)
    {
        yield return english;
        if (!Strings.TryGetValue(english, out var row)) yield break;
        foreach (var text in row) yield return text;
    }

    /// <summary>
    /// Resolve uma tradução conhecida de volta para sua chave inglesa. Isso permite que
    /// textos atualizados dinamicamente pela aplicação continuem traduzíveis ao trocar idioma.
    /// </summary>
    public static string? GetEnglishKey(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (Strings.ContainsKey(text)) return text;

        foreach (var (english, row) in Strings)
            if (row.Any(translation => string.Equals(translation, text, StringComparison.Ordinal)))
                return english;

        return null;
    }

    private static readonly Dictionary<string, string[]> Strings = new(StringComparer.Ordinal)
    {
        ["RM Monitor"] = ["RM Monitor", "RM Monitor", "RM Monitor", "RM Monitor", "RM Monitor"],
        ["HW Metrics — Hardware Studio"] = ["HW Metrics — Central de Hardware", "HW Metrics — Centro de Hardware", "HW Metrics — Centro Hardware", "HW Metrics — Centre matériel", "HW Metrics — 硬件中心"],
        ["HARDWARE STUDIO"] = ["CENTRAL DE HARDWARE", "CENTRO DE HARDWARE", "CENTRO HARDWARE", "CENTRE MATÉRIEL", "硬件中心"],
        ["Minimize"] = ["Minimizar", "Minimizar", "Riduci a icona", "Réduire", "最小化"],
        ["Maximize / Restore"] = ["Maximizar / Restaurar", "Maximizar / Restaurar", "Ingrandisci / Ripristina", "Agrandir / Restaurer", "最大化 / 还原"],
        ["Close settings"] = ["Fechar configurações", "Cerrar configuración", "Chiudi impostazioni", "Fermer les paramètres", "关闭设置"],
        ["Changes the preview appearance only; it does not control the hardware."] = ["Altera apenas a aparência da prévia; não controla o hardware.", "Solo cambia el aspecto de la vista previa; no controla el hardware.", "Cambia solo l'aspetto dell'anteprima; non controlla l'hardware.", "Modifie uniquement l'aspect de l'aperçu ; ne pilote pas le matériel.", "仅更改预览外观，不会控制硬件。"],
        ["Detecting processor..."] = ["Detectando processador...", "Detectando procesador...", "Rilevamento del processore...", "Détection du processeur...", "正在检测处理器..."],
        ["Detecting graphics..."] = ["Detectando placa de vídeo...", "Detectando gráficos...", "Rilevamento della scheda grafica...", "Détection de la carte graphique...", "正在检测显卡..."],
        ["Detecting drive..."] = ["Detectando unidade...", "Detectando unidad...", "Rilevamento dell'unità...", "Détection du disque...", "正在检测磁盘..."],
        ["Waiting for memory data"] = ["Aguardando dados da memória", "Esperando datos de la memoria", "In attesa dei dati della memoria", "En attente des données de la mémoire", "正在等待内存数据"],
        ["Utilization --%"] = ["Utilização --%", "Uso --%", "Utilizzo --%", "Utilisation --%", "使用率 --%"],
        ["Open RM Monitor"] = ["Abrir RM Monitor", "Abrir RM Monitor", "Apri RM Monitor", "Ouvrir RM Monitor", "打开 RM Monitor"],
        ["Open HW Metrics"] = ["Abrir HW Metrics", "Abrir HW Metrics", "Apri HW Metrics", "Ouvrir HW Metrics", "打开 HW Metrics"],
        ["Exit"] = ["Sair", "Salir", "Esci", "Quitter", "退出"],
        ["WORKSPACE"] = ["ESPAÇO DE TRABALHO", "ESPACIO DE TRABAJO", "AREA DI LAVORO", "ESPACE DE TRAVAIL", "工作区"],
        ["Overview"] = ["Visão geral", "Resumen", "Panoramica", "Aperçu", "概览"],
        ["Cooling"] = ["Refrigeração", "Refrigeración", "Raffreddamento", "Refroidissement", "散热"],
        ["Hardware"] = ["Hardware", "Hardware", "Hardware", "Matériel", "硬件"],
        ["Settings"] = ["Configurações", "Configuración", "Impostazioni", "Paramètres", "设置"],
        ["DISPLAY DEVICE"] = ["DISPOSITIVO DE DISPLAY", "DISPOSITIVO DE PANTALLA", "DISPOSITIVO DISPLAY", "PÉRIPHÉRIQUE D'AFFICHAGE", "显示设备"],
        ["Waiting for device"] = ["Aguardando dispositivo", "Esperando dispositivo", "In attesa del dispositivo", "En attente de l'appareil", "等待设备"],
        ["USB / HID connection not enabled"] = ["Conexão USB / HID desativada", "Conexión USB / HID desactivada", "Connessione USB / HID non attiva", "Connexion USB / HID désactivée", "未启用 USB / HID 连接"],
        ["HID display connected"] = ["Display HID conectado", "Pantalla HID conectada", "Display HID connesso", "Écran HID connecté", "HID 显示屏已连接"],
        ["Telemetry sent to physical display"] = ["Telemetria enviada ao display físico", "Telemetría enviada a la pantalla física", "Telemetria inviata al display fisico", "Télémétrie envoyée à l'écran physique", "遥测数据已发送到物理显示屏"],
        ["HID display not found"] = ["Display HID não encontrado", "Pantalla HID no encontrada", "Display HID non trovato", "Écran HID introuvable", "未找到 HID 显示屏"],
        ["CPU temperature unavailable · not sent"] = ["Temperatura da CPU indisponível · não enviado", "Temperatura de la CPU no disponible · no enviado", "Temperatura della CPU non disponibile · non inviato", "Température du CPU indisponible · non envoyé", "CPU 温度不可用 · 未发送"],
        ["HID report incompatible"] = ["Relatório HID incompatível", "Informe HID incompatible", "Report HID non compatibile", "Rapport HID incompatible", "HID 报告不兼容"],
        ["Could not open HID display"] = ["Não foi possível abrir o display HID", "No se pudo abrir la pantalla HID", "Impossibile aprire il display HID", "Impossible d'ouvrir l'écran HID", "无法打开 HID 显示屏"],
        ["HID write failed"] = ["Falha ao transmitir por HID", "Error al transmitir por HID", "Trasmissione HID non riuscita", "Échec de l'écriture HID", "HID 写入失败"],
        ["Interface theme"] = ["Tema da interface", "Tema de la interfaz", "Tema dell'interfaccia", "Thème de l'interface", "界面主题"],
        ["Dark"] = ["Escuro", "Oscuro", "Scuro", "Sombre", "深色"],
        ["Light"] = ["Claro", "Claro", "Chiaro", "Clair", "浅色"],
        ["Refresh rate"] = ["Intervalo de atualização", "Intervalo de actualización", "Intervallo di aggiornamento", "Intervalle d'actualisation", "刷新间隔"],
        ["Language"] = ["Idioma", "Idioma", "Lingua", "Langue", "语言"],
        ["SYSTEM SETTINGS"] = ["CONFIGURAÇÕES DO SISTEMA", "AJUSTES DEL SISTEMA", "IMPOSTAZIONI DI SISTEMA", "PARAMÈTRES SYSTÈME", "系统设置"],
        ["Minimize to tray on close"] = ["Minimizar para a bandeja ao fechar", "Minimizar a la bandeja al cerrar", "Riduci nell'area di notifica alla chiusura", "Réduire dans la zone de notification à la fermeture", "关闭时最小化到托盘"],
        ["Keep HW Metrics running in the notification area"] = ["Manter o HW Metrics em execução na área de notificação", "Mantener HW Metrics en ejecución en el área de notificación", "Mantieni HW Metrics in esecuzione nell'area di notifica", "Garder HW Metrics actif dans la zone de notification", "让 HW Metrics 在通知区域中继续运行"],
        ["Start with Windows"] = ["Iniciar com o Windows", "Iniciar con Windows", "Avvia con Windows", "Démarrer avec Windows", "随 Windows 启动"],
        ["Launch automatically in the tray when you sign in"] = ["Abrir automaticamente na bandeja ao entrar no Windows", "Abrir automáticamente en la bandeja al iniciar sesión", "Avvia automaticamente nell'area di notifica all'accesso", "Se lancer automatiquement dans la zone de notification à l'ouverture de session", "登录时自动在托盘中启动"],
        ["1 sec"] = ["1 s", "1 s", "1 s", "1 s", "1 秒"],
        ["2 sec"] = ["2 s", "2 s", "2 s", "2 s", "2 秒"],
        ["5 sec"] = ["5 s", "5 s", "5 s", "5 s", "5 秒"],
        ["Preview build · .NET 10"] = ["Versão de prévia · .NET 10", "Versión preliminar · .NET 10", "Versione di anteprima · .NET 10", "Version préliminaire · .NET 10", "预览版 · .NET 10"],
        ["System overview"] = ["Visão geral do sistema", "Resumen del sistema", "Panoramica del sistema", "Aperçu du système", "系统概览"],
        ["Your hardware, at a glance."] = ["Seu hardware em um só lugar.", "Tu hardware, de un vistazo.", "Il tuo hardware, a colpo d'occhio.", "Votre matériel, en un coup d'œil.", "您的硬件，一目了然。"],
        ["CPU"] = ["CPU", "CPU", "CPU", "CPU", "CPU"],
        ["GPU"] = ["GPU", "GPU", "GPU", "GPU", "GPU"],
        ["MEMORY"] = ["MEMÓRIA", "MEMORIA", "MEMORIA", "MÉMOIRE", "内存"],
        ["Memory model unavailable"] = ["Modelo de memória indisponível", "Modelo de memoria no disponible", "Modello della memoria non disponibile", "Modèle de mémoire indisponible", "内存型号不可用"],
        ["Capacity unavailable"] = ["Capacidade indisponível", "Capacidad no disponible", "Capacità non disponibile", "Capacité indisponible", "容量不可用"],
        ["STORAGE"] = ["ARMAZENAMENTO", "ALMACENAMIENTO", "ARCHIVIAZIONE", "STOCKAGE", "存储"],
        ["Waiting for disk data"] = ["Aguardando dados do disco", "Esperando datos del disco", "In attesa dei dati del disco", "En attente des données du disque", "正在等待磁盘数据"],
        ["Rolling 30 minutes · data since launch"] = ["Últimos 30 minutos · dados desde a inicialização", "Últimos 30 minutos · datos desde el inicio", "Ultimi 30 minuti · dati dall'avvio", "30 dernières minutes · données depuis le lancement", "最近 30 分钟 · 自启动以来的数据"],
        ["100%"] = ["100%", "100%", "100%", "100%", "100%"],
        ["75%"] = ["75%", "75%", "75%", "75%", "75%"],
        ["50%"] = ["50%", "50%", "50%", "50%", "50%"],
        ["25%"] = ["25%", "25%", "25%", "25%", "25%"],
        ["Temperature"] = ["Temperatura", "Temperatura", "Temperatura", "Température", "温度"],
        ["Live temperature history"] = ["Histórico da temperatura em tempo real", "Historial de temperatura en tiempo real", "Cronologia della temperatura in tempo reale", "Historique de température en direct", "实时温度历史"],
        ["Utilization"] = ["Utilização", "Uso", "Utilizzo", "Utilisation", "使用率"],
        ["Live utilization history"] = ["Histórico da utilização em tempo real", "Historial de uso en tiempo real", "Cronologia dell'utilizzo in tempo reale", "Historique d'utilisation en direct", "实时使用率历史"],
        ["WATER COOLER MODEL"] = ["MODELO DO WATER COOLER", "MODELO DE WATER COOLER", "MODELLO DI WATER COOLER", "MODÈLE DE WATERCOOLING", "水冷型号"],
        ["Preferences"] = ["Preferências", "Preferencias", "Preferenze", "Préférences", "偏好设置"],
        ["Choose light or dark appearance"] = ["Escolha o tema claro ou escuro", "Elige el tema claro u oscuro", "Scegli l'aspetto chiaro o scuro", "Choisissez l'apparence claire ou sombre", "选择浅色或深色外观"],
        ["Sensor polling interval"] = ["Intervalo de leitura dos sensores", "Intervalo de lectura de sensores", "Intervallo di lettura dei sensori", "Intervalle de lecture des capteurs", "传感器读取间隔"],
        ["Application display language"] = ["Idioma da interface", "Idioma de la aplicación", "Lingua dell'applicazione", "Langue de l'application", "应用显示语言"],
        ["Close"] = ["Fechar", "Cerrar", "Chiudi", "Fermer", "关闭"],
        ["Pump display preview"] = ["Prévia do display da bomba", "Vista previa de la pantalla de la bomba", "Anteprima del display della pompa", "Aperçu de l'écran de la pompe", "水泵显示屏预览"],
        ["Thermal components"] = ["Componentes térmicos", "Componentes térmicos", "Componenti termici", "Composants thermiques", "温度组件"],
        ["Sensor availability on this machine"] = ["Sensores disponíveis neste computador", "Sensores disponibles en este equipo", "Sensori disponibili su questo computer", "Capteurs disponibles sur cet ordinateur", "此计算机上可用的传感器"],
        ["CPU package"] = ["Pacote da CPU", "Paquete de la CPU", "Package della CPU", "Package du CPU", "CPU 封装"],
        ["Processor temperature"] = ["Temperatura do processador", "Temperatura del procesador", "Temperatura del processore", "Température du processeur", "处理器温度"],
        ["GPU core"] = ["Núcleo da GPU", "Núcleo de la GPU", "Core della GPU", "Cœur du GPU", "GPU 核心"],
        ["Graphics temperature"] = ["Temperatura gráfica", "Temperatura de los gráficos", "Temperatura della scheda grafica", "Température de la carte graphique", "显卡温度"],
        ["Storage"] = ["Armazenamento", "Almacenamiento", "Archiviazione", "Stockage", "存储"],
        ["Drive temperature, if exposed"] = ["Temperatura do disco, se disponível", "Temperatura del disco, si está disponible", "Temperatura del disco, se disponibile", "Température du disque, si disponible", "磁盘温度（如可用）"],
        ["Storage volumes will appear here."] = ["Os volumes de armazenamento aparecerão aqui.", "Los volúmenes de almacenamiento aparecerán aquí.", "I volumi di archiviazione appariranno qui.", "Les volumes de stockage apparaîtront ici.", "存储卷将显示在此处。"],
        ["Fan and pump control will appear after protocol validation."] = ["O controle de ventoinhas e bomba será disponibilizado após validar o protocolo.", "El control de ventiladores y bomba estará disponible tras validar el protocolo.", "Il controllo di ventole e pompa sarà disponibile dopo la convalida del protocollo.", "Le contrôle des ventilateurs et de la pompe sera disponible après validation du protocole.", "风扇和水泵控制将在协议验证后提供。"],
        ["No local volumes detected"] = ["Nenhum volume local detectado", "No se detectaron volúmenes locales", "Nessun volume locale rilevato", "Aucun volume local détecté", "未检测到本地卷"],
        ["Storage capacity unavailable"] = ["Capacidade do armazenamento indisponível", "Capacidad de almacenamiento no disponible", "Capacità di archiviazione non disponibile", "Capacité de stockage indisponible", "存储容量不可用"],
        ["total"] = ["Total", "total", "totale", "total", "总计"],
        ["free"] = ["livres", "libres", "liberi", "libres", "可用"],
        ["Save history"] = ["Salvar histórico", "Guardar historial", "Salva cronologia", "Enregistrer l'historique", "保存历史"],
        ["History retention"] = ["Retenção do histórico", "Retención del historial", "Conservazione della cronologia", "Conservation de l'historique", "历史保留时长"],
        ["How long to keep telemetry locally"] = ["Por quanto tempo manter a telemetria localmente", "Cuánto tiempo conservar la telemetría localmente", "Per quanto tempo conservare la telemetria in locale", "Durée de conservation locale de la télémétrie", "遥测数据在本地保留多久"],
        ["30 minutes"] = ["30 minutos", "30 minutos", "30 minuti", "30 minutes", "30 分钟"],
        ["1 hour"] = ["1 hora", "1 hora", "1 ora", "1 heure", "1 小时"],
        ["6 hours"] = ["6 horas", "6 horas", "6 ore", "6 heures", "6 小时"],
        ["24 hours"] = ["24 horas", "24 horas", "24 ore", "24 heures", "24 小时"],
        ["Drive model unavailable"] = ["Modelo do disco indisponível", "Modelo de disco no disponible", "Modello del disco non disponibile", "Modèle du disque indisponible", "磁盘型号不可用"],
        ["Processor model unavailable"] = ["Modelo do processador indisponível", "Modelo del procesador no disponible", "Modello del processore non disponibile", "Modèle du processeur indisponible", "处理器型号不可用"],
        ["Graphics model unavailable"] = ["Modelo gráfico indisponível", "Modelo de gráficos no disponible", "Modello della scheda grafica non disponibile", "Modèle de la carte graphique indisponible", "显卡型号不可用"],
        ["Utilization "] = ["Utilização ", "Uso ", "Utilizzo ", "Utilisation ", "使用率 "],
        ["Select at least one series to export."] = ["Selecione ao menos uma série para exportar.", "Selecciona al menos una serie para exportar.", "Seleziona almeno una serie da esportare.", "Sélectionnez au moins une série à exporter.", "请至少选择一个要导出的数据系列。"],
        ["Nothing to export"] = ["Nada para exportar", "Nada que exportar", "Niente da esportare", "Rien à exporter", "没有可导出的内容"],
        ["Export history to CSV"] = ["Exportar histórico para CSV", "Exportar historial a CSV", "Esporta cronologia in CSV", "Exporter l'historique en CSV", "将历史导出为 CSV"],
        ["History exported successfully."] = ["Histórico exportado com sucesso.", "Historial exportado correctamente.", "Cronologia esportata correttamente.", "Historique exporté avec succès.", "历史已成功导出。"],
        ["Export complete"] = ["Exportação concluída", "Exportación completada", "Esportazione completata", "Exportation terminée", "导出完成"],
        ["Could not export history."] = ["Não foi possível exportar o histórico.", "No se pudo exportar el historial.", "Impossibile esportare la cronologia.", "Impossible d'exporter l'historique.", "无法导出历史。"],
        ["Export failed"] = ["Falha na exportação", "Error al exportar", "Esportazione non riuscita", "Échec de l'exportation", "导出失败"],
        ["Would you like to exit HW Metrics or keep it running in the system tray?"] = ["Deseja fechar o HW Metrics ou mantê-lo em execução na bandeja do sistema?", "¿Quieres salir de HW Metrics o mantenerlo en ejecución en la bandeja del sistema?", "Vuoi chiudere HW Metrics o lasciarlo in esecuzione nell'area di notifica?", "Voulez-vous quitter HW Metrics ou le garder actif dans la zone de notification ?", "要退出 HW Metrics，还是让它继续在系统托盘中运行？"],
        ["Close HW Metrics"] = ["Fechar HW Metrics", "Cerrar HW Metrics", "Chiudi HW Metrics", "Fermer HW Metrics", "关闭 HW Metrics"],
        ["Could not update Windows startup settings."] = ["Não foi possível alterar a inicialização do Windows.", "No se pudo actualizar la configuración de inicio de Windows.", "Impossibile aggiornare le impostazioni di avvio di Windows.", "Impossible de mettre à jour les paramètres de démarrage de Windows.", "无法更新 Windows 启动设置。"],
        ["Startup setting failed"] = ["Falha na configuração de inicialização", "Error en la configuración de inicio", "Impostazione di avvio non riuscita", "Échec du paramètre de démarrage", "启动设置失败"],
        ["{0} used · {1} free of {2}"] = ["{0} em uso · {1} livres de {2}", "{0} en uso · {1} libres de {2}", "{0} in uso · {1} liberi su {2}", "{0} utilisés · {1} libres sur {2}", "已用 {0} · 剩余 {1}（共 {2}）"],
    };
}
