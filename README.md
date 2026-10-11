# HW Metrics

A modern WPF hardware dashboard targeting **.NET 10**. The interface supports **Portuguese and English**, dark/light themes, animated transitions, live sensor polling, and a rolling temperature history chart. Temperatures are displayed in Celsius only.

## Features / Funcionalidades

- **Language / Idioma:** English and Português, switchable in Settings / alternância em Configurações.
- **CPU and GPU:** temperatures and utilization when exposed by the hardware/driver / temperaturas e utilização quando disponíveis.
- **RAM:** usage and total capacity via Windows memory API fallback / uso e capacidade total com alternativa pela API do Windows.
- **CPU telemetry fallback:** CPU temperature prefers package/die/core sensors and can fall back to a motherboard/Super I/O sensor explicitly labelled CPU or processor. CPU utilization falls back to Windows system-time counters when LibreHardwareMonitor does not provide a CPU load sensor. Temperature still depends on firmware/driver sensor exposure; some systems do not expose a readable CPU temperature.
- **Cooler display:** CPU/GPU temperature and CPU/GPU/RAM utilization are encoded into the recovered 11-byte telemetry payload and sent directly over USB/HID to VID `AA88`, PID `8666`. The tested device advertises an 11-byte output report; the app writes the 11-byte payload directly, without prepending a separate Report ID byte. CPU temperature must be valid before any report is sent. The app preview mirrors the same telemetry snapshot; fan and pump control remain disabled. A completed HID write does not guarantee that the physical display rendered the values.
- **Storage:** system drive usage percentage, used/total/free capacity, and a summary of detected fixed volumes. Drive temperature is shown separately only when exposed by the controller / percentual usado, capacidade usada/total/livre e resumo dos volumes detectados. A temperatura do disco é separada e depende do controlador.
- **Telemetry history:** local rolling history with a 30-minute default retention, configurable to 1, 6, or 24 hours in Settings. Expired and future-dated snapshots are removed from the local JSON file. Use **Save history / Salvar histórico** to export a separate JSON copy / histórico local com retenção padrão de 30 minutos, configurável para 1, 6 ou 24 horas em Configurações. Registros expirados ou com data futura são removidos do arquivo JSON local. Use **Salvar histórico** para exportar uma cópia JSON separada.
- **Live charts:** temperature and utilization charts show the latest 30 minutes; longer local retention is available for history export / os gráficos mostram os últimos 30 minutos; retenções locais maiores ficam disponíveis para exportação.
- Manual refresh from the dashboard (button or F5), Escape closes Settings, and polling intervals of 1, 2, or 5 seconds; keyboard focus is visible in navigation controls / atualização manual no painel (botão ou F5), Escape fecha Configurações e intervalos de 1, 2 ou 5 segundos; o foco do teclado fica visível nos controles de navegação.
- Dark and light themes / temas claro e escuro.
- Direct USB/HID telemetry output to the matching cooler; fan/pump control remains disabled / envio direto da telemetria USB/HID ao water cooler compatível; o controle da bomba/ventoinha continua desativado.

## Requirements / Requisitos

- Windows 10/11
- .NET 10 SDK
- Internet connection for the first NuGet restore / conexão com a internet na primeira restauração do NuGet

## Restore, build, run / Restaurar, compilar e executar

```powershell
dotnet restore .\WaterCoolerApp.csproj
dotnet build .\WaterCoolerApp.csproj -c Release
dotnet run --project .\WaterCoolerApp.csproj -c Release
```

## Publish self-contained x64 / Publicar independente x64

```powershell
dotnet publish .\WaterCoolerApp.csproj -c Release -r win-x64 --self-contained true
```

## Windows x64 CI artifact / Artefato Windows x64 pela CI

To create a downloadable self-contained Windows x64 build, open **Actions → Publish Windows x64 artifact → Run workflow** on GitHub. The workflow runs the test suite, publishes the app for `win-x64`, and uploads an artifact named `HW-Metrics-win-x64` retained for 14 days. Download and extract the artifact, then launch `HWMetrics.exe` on Windows 10/11. This is a portable publish folder, not an installer.

Para gerar uma compilação independente para Windows x64, abra **Actions → Publish Windows x64 artifact → Run workflow** no GitHub. O workflow executa os testes, publica o aplicativo para `win-x64` e envia um artefato chamado `HW-Metrics-win-x64`, mantido por 14 dias. Baixe e extraia o artefato e execute `HWMetrics.exe` no Windows 10/11. O resultado é uma pasta portátil publicada, não um instalador.

## Storage notes / Observações sobre armazenamento

The Storage card reports filesystem capacity for ready fixed volumes (the Windows system volume is prioritized). This is different from SMART/NVMe drive temperature: temperature is displayed only if LibreHardwareMonitor and the storage controller expose it. If a drive is absent from the summary, confirm that Windows has mounted it and assigned a drive letter.

O cartão Armazenamento mostra a capacidade do sistema de arquivos dos volumes fixos prontos (priorizando a unidade do Windows). Isso é diferente da temperatura SMART/NVMe: a temperatura só aparece quando o LibreHardwareMonitor e o controlador a disponibilizam. Se uma unidade não aparecer no resumo, confirme que ela está montada no Windows e possui uma letra atribuída.

## Telemetry history storage / Armazenamento do histórico de telemetria

The app stores a bounded rolling history at `%LocalAppData%\\HWMetrics\\telemetry-history.json`. By default, only the latest 30 minutes are retained; Settings can extend retention to 1, 6, or 24 hours. The file is periodically rewritten with only snapshots inside the selected window, and invalid future-dated or expired records are discarded. This bounds the automatic local history rather than accumulating an unbounded log. **Save history / Salvar histórico** exports a separate JSON copy to a location selected by the user; that exported copy is not automatically pruned by the app.

O aplicativo mantém um histórico local rotativo em `%LocalAppData%\\HWMetrics\\telemetry-history.json`. Por padrão, guarda apenas os últimos 30 minutos; em Configurações, a retenção pode ser ampliada para 1, 6 ou 24 horas. O arquivo é regravado periodicamente apenas com os registros dentro do período selecionado, descartando registros expirados ou com data futura. Assim, o histórico automático não cresce indefinidamente. **Salvar histórico** exporta uma cópia JSON separada para o local escolhido pelo usuário; essa cópia exportada não é apagada automaticamente pelo aplicativo.

## Safety / Segurança

The app sends only the reconstructed telemetry report to a matching HID device after checking its output-report length. Fan and pump control commands are not implemented. A successful HID write confirms that Windows accepted the report, not that the physical display rendered it correctly; test with the actual cooler before relying on the display.
