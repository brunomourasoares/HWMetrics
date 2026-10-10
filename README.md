# Aqua Core — Water Cooler App

A modern WPF hardware dashboard targeting **.NET 10**. The interface supports **Portuguese and English**, dark/light themes, animated transitions, live sensor polling, and a rolling temperature history chart. Temperatures are displayed in Celsius only.

## Features / Funcionalidades

- **Language / Idioma:** English and Português, switchable in Settings / alternância em Configurações.
- **CPU and GPU:** temperatures and utilization when exposed by the hardware/driver / temperaturas e utilização quando disponíveis.
- **RAM:** usage and total capacity via Windows memory API fallback / uso e capacidade total com alternativa pela API do Windows.
- **Storage:** system drive usage percentage, used/total/free capacity, and a summary of detected fixed volumes. Drive temperature is shown separately only when exposed by the controller / percentual usado, capacidade usada/total/livre e resumo dos volumes detectados. A temperatura do disco é separada e depende do controlador.
- **Live chart:** CPU/GPU temperature history for up to 30 minutes since app launch / histórico de temperatura da CPU/GPU por até 30 minutos desde a inicialização.
- Manual refresh and polling intervals of 1, 2, or 5 seconds / atualização manual e intervalos de 1, 2 ou 5 segundos.
- Dark and light themes / temas claro e escuro.
- UI-only water cooler display preview. **No USB/HID writes are enabled** / prévia visual do display. **Nenhum comando USB/HID é enviado**.

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

## Storage notes / Observações sobre armazenamento

The Storage card reports filesystem capacity for ready fixed volumes (the Windows system volume is prioritized). This is different from SMART/NVMe drive temperature: temperature is displayed only if LibreHardwareMonitor and the storage controller expose it. If a drive is absent from the summary, confirm that Windows has mounted it and assigned a drive letter.

O cartão Armazenamento mostra a capacidade do sistema de arquivos dos volumes fixos prontos (priorizando a unidade do Windows). Isso é diferente da temperatura SMART/NVMe: a temperatura só aparece quando o LibreHardwareMonitor e o controlador a disponibilizam. Se uma unidade não aparecer no resumo, confirme que ela está montada no Windows e possui uma letra atribuída.

## Safety / Segurança

The app currently reads telemetry only. HID output, pump control, and fan control remain disabled until the device protocol is physically validated. Do not assume a visual preview means the cooler hardware has received data.
