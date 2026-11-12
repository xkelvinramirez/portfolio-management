# Harness decisions

- Accepted plan `b1f65b3f06e28a2b87603d1abdde629b43baeb4a1eafae7f919e3362a3ade02e`.
- Project kind: software.
- SDD: selected.

- 2026-11-12 (maf-tool-calling-agent): una fecha sin hora que llega a una herramienta del agente MAF cuenta como ese día a las 00:00 UTC (no medianoche de Madrid), en entradas y en el periodo del informe. Motivo: Domain e informes agrupan por día UTC.
- 2026-11-12 (maf-tool-calling-agent): las fechas llegan a las herramientas como texto, no como `DateTime`. Con Z u offset = ese instante; con hora y sin offset = hora de Europe/Madrid; una hora en el salto de verano = `MissingOrAmbiguous`. Motivo: el binder de `AIFunctionFactory` no aplica la zona de negocio.
- 2026-11-12 (maf-tool-calling-agent): el límite de pasos (`Ai:MaxAllowedToolLoops`) lo aplica un middleware que cuenta las llamadas y termina el bucle con un error propio. Motivo: `MaximumIterationsPerRequest` corta en silencio y el usuario no sabría que la respuesta está incompleta.
- 2026-11-12 (maf-tool-calling-agent): solo el pipeline Polly `InferenceClient` (`Ai:RequestTimeoutSeconds`) corta y reintenta. HttpClient y el SDK de OpenAI/Azure llevan timeout infinito y 0 reintentos. Motivo: las capas apiladas daban esperas de ~400 s y un 500.
- 2026-11-12 (maf-tool-calling-agent): cada turno inyecta los portafolios del usuario con su id real. Motivo: el modelo local inventaba `portfolioId: 1`.
- 2026-11-12 (maf-tool-calling-agent): `qwen2.5:7b-instruct` es el modelo local mínimo del agente MAF (checklist 9/9; 3B solo fiable en lectura). Va en los user-secrets de cada desarrollador; `appsettings.Development.json` no cambia.
