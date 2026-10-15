# Hardware Validation Checklist / Lista de validação de hardware

This checklist supplements automated CI. It is intended for a Windows 10/11 machine with the target cooler physically connected. A successful build or HID write is not proof that the physical display rendered the report correctly.

Esta lista complementa a CI automatizada. Deve ser executada em um computador Windows 10/11 com o water cooler compatível conectado. Compilar o aplicativo ou concluir uma escrita HID não comprova que o display físico interpretou e exibiu o relatório corretamente.

## Safety boundaries / Limites de segurança

- The app is telemetry-only. Do not add or send pump/fan speed commands as part of this validation.
- Keep the cooler connected to its normal motherboard/controller power and control connections. Do not disconnect pump or fan power to test this app.
- Stop the test if cooling hardware behaves unexpectedly, temperatures rise abnormally, or the device behaves differently from its normal operation.
- Record the cooler model, Windows version, app commit/build, and whether the run used a self-contained x64 publish.
- A HID status of `Sent` means the write completed without an exception; it is not device acknowledgement.

- O aplicativo envia apenas telemetria. Não adicione nem envie comandos de velocidade da bomba/ventoinhas durante a validação.
- Mantenha o cooler ligado às conexões normais de alimentação e controle da placa-mãe/controladora. Não desconecte a alimentação da bomba ou das ventoinhas para testar o aplicativo.
- Interrompa o teste se o sistema de refrigeração se comportar de forma inesperada, se as temperaturas subirem anormalmente ou se o dispositivo sair do comportamento normal.
- Registre o modelo do cooler, a versão do Windows, o commit/build do aplicativo e se foi usado o pacote x64 independente.
- O estado HID `Sent` significa que a escrita terminou sem exceção; não representa confirmação do dispositivo.

## Preconditions / Pré-requisitos

- [ ] Windows 10/11 x64.
- [ ] Target cooler is connected and operating normally before launching the app.
- [ ] Use the artifact produced by **Actions → Publish Windows x64 artifact**, or build using the README instructions.
- [ ] Record the starting CPU temperature and verify the cooler is operating normally in its usual monitoring/control software.

## Test cases / Casos de teste

| ID | Action / Ação | Expected result / Resultado esperado | Pass |
| --- | --- | --- | --- |
| HW-01 | Launch RM Monitor normally. / Inicie o RM Monitor normalmente. | Window opens; no unhandled exception or startup crash. / A janela abre sem exceção não tratada ou falha de inicialização. | [ ] |
| HW-02 | Observe CPU, GPU, RAM and storage cards for at least 2 minutes. / Observe os cartões por pelo menos 2 minutos. | Values update at the selected interval; unavailable sensors are shown as unavailable rather than fabricated values. / Valores são atualizados no intervalo selecionado; sensores indisponíveis são indicados como indisponíveis. | [ ] |
| HW-03 | Change polling interval between 1, 2 and 5 seconds. / Alterne entre 1, 2 e 5 segundos. | UI remains responsive and polling does not overlap or freeze. / A interface permanece responsiva e as leituras não se sobrepõem nem travam. | [ ] |
| HW-04 | Confirm the cooler's physical display while telemetry changes naturally. Do not stress the CPU solely for this test. / Confira o display físico enquanto a telemetria variar naturalmente; não force a CPU só para este teste. | Display values correspond to the app preview within the device's update cadence and integer rounding. / Valores correspondem à prévia do aplicativo, considerando o tempo de atualização e o arredondamento para inteiros. | [ ] |
| HW-05 | Compare reported temperatures with a trusted existing hardware monitor. / Compare as temperaturas com um monitor de hardware confiável já utilizado. | Differences are explainable by sensor source and update cadence; no implausible jumps. / Diferenças são explicáveis pela origem do sensor e pelo intervalo de atualização; não há saltos implausíveis. | [ ] |
| HW-06 | Open Settings, change theme/language, then restart. / Abra Configurações, altere tema/idioma e reinicie. | Preferences persist and the app starts without errors. / Preferências persistem e o aplicativo inicia sem erros. | [ ] |
| HW-07 | Use the window close action and choose to keep the app in the system tray; restore it from the tray. / Feche a janela escolhendo manter o app na bandeja e restaure-o pela bandeja. | App continues polling while hidden and restores without opening a second instance or losing responsiveness. / O app continua lendo sensores oculto e restaura sem abrir outra instância nem perder responsividade. | [ ] |
| HW-08 | Exit the app explicitly. Relaunch and inspect the recent history chart. / Encerre explicitamente o app, reinicie e confira o gráfico de histórico recente. | App exits cleanly; recent local history loads and old/future records are not shown. / O app encerra corretamente; o histórico recente é carregado e registros antigos/futuros não aparecem. | [ ] |
| HW-09 | If safe and practical, close the app, disconnect only the USB/HID data connection, relaunch, and observe status; close the app before reconnecting. Do not disturb power/control cables. / Se for seguro e viável, feche o app, desconecte somente o cabo de dados USB/HID, reinicie e observe o estado; feche o app antes de reconectar. Não mexa nos cabos de alimentação/controle. | Missing-device status is shown without an app crash; after reconnection and relaunch, the interface can be detected again. / Estado de dispositivo ausente é exibido sem travar o app; após reconectar e reiniciar, a interface pode ser detectada novamente. | [ ] |

## Evidence / Evidências

For each failed case, record the test ID, exact steps, expected and observed behavior, app commit/build, cooler model, and relevant log entries. The HID diagnostic log is located at:

Para cada caso que falhar, registre o ID, passos exatos, comportamento esperado e observado, commit/build, modelo do cooler e entradas de log relevantes. O log de diagnóstico HID fica em:

`%LocalAppData%\RM Monitor\hid-diagnostic.log`

Do not include personal data from unrelated files. A passing checklist documents one specific hardware/software configuration; it does not guarantee compatibility with every cooler revision or motherboard.
