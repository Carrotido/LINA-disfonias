# GitHub Copilot Instructions — Vocalis FonoPlay (Godot)

## Propósito
Vocalis FonoPlay es una aplicación de escritorio para ejercicios de rehabilitación vocal mediante gamificación y biofeedback acústico.

Debe permitir:
- ejercicios interactivos para pacientes;
- captura de audio mediante micrófono;
- procesamiento acústico en tiempo real;
- extracción de características;
- transformación a biofeedback;
- control de ejercicios;
- registro de sesiones, métricas y resultados;
- funcionamiento local/offline-first en el MVP;
- evolución futura hacia backend y sincronización.

Prioridades: estabilidad, mantenibilidad, bajo acoplamiento, claridad y evolución incremental.

## Stack
Usar:
- Godot 4.x .NET
- C#
- .NET SDK compatible con la versión instalada
- Visual Studio Code
- Git / GitHub
- SQLite para persistencia local

Arquitectura futura:
- ASP.NET Core Web API
- C# / .NET LTS
- Entity Framework Core
- PostgreSQL
- React + TypeScript

No introducir tecnologías adicionales sin necesidad concreta. No usar microservicios, Kubernetes, Redis, RabbitMQ ni infraestructura compleja en el MVP.

## Godot, no Unity
El proyecto migró de Unity a Godot.

NO utilizar:
- UnityEngine
- MonoBehaviour
- ScriptableObject
- UnityEngine.Microphone
- Unity Audio API
- patrones específicos de Unity

No traducir literalmente soluciones de Unity. Adaptarlas al modelo idiomático de Godot y C#.

## Arquitectura general
Mantener:

Micrófono
    ↓
AudioCapture
    ↓
AudioProcessor
    ↓
FeatureExtractor
    ↓
BiofeedbackEngine
    ↓
ExerciseController
    ↓
Juego / UI
    ↓
Resultado
    ↓
SessionManager
    ↓
Persistencia

REGLA CRÍTICA: el micrófono nunca debe controlar directamente un ejercicio, animación o UI.

## Estructura inicial
res://
├── Scenes/
├── Scripts/
│   ├── Audio/
│   │   ├── Capture/
│   │   ├── Processing/
│   │   └── Features/
│   ├── Biofeedback/
│   ├── Exercises/
│   │   ├── Core/
│   │   ├── Balloon/
│   │   └── Bird/
│   ├── Clinical/
│   ├── Data/
│   ├── UI/
│   └── Core/
├── Resources/
├── Assets/
│   ├── Art/
│   ├── Audio/
│   └── Fonts/
├── Tests/
└── Documentation/

Puede modificarse si una solución más idiomática de Godot lo justifica. No crear estructura innecesaria.

## Responsabilidades

### AudioCapture
Responsable de:
- inicializar captura;
- iniciar/detener micrófono;
- comprobar disponibilidad;
- obtener muestras;
- informar estado y errores.

No calcula RMS, puntuaciones ni controla ejercicios/UI.

### AudioProcessor
Recibe y procesa muestras. Debe poder evolucionar hacia:
- filtrado;
- reducción de ruido;
- ventanas;
- normalización;
- procesamiento espectral.

No conoce la lógica de los ejercicios.

### FeatureExtractor
Extrae inicialmente:
- RMS;
- energía;
- amplitud;
- duración;
- actividad sonora.

Posteriormente:
- F0;
- pitch;
- FFT;
- espectro;
- espectrograma;
- otras métricas acústicas.

### BiofeedbackEngine
Convierte características acústicas en valores utilizables por los ejercicios.

Ejemplo:
RMS → normalización → threshold → smoothing → valor 0..1

Debe ser genérico y no saber si el consumidor es un globo, pájaro u otro ejercicio.

### ExerciseController
Contiene la lógica específica del ejercicio y consume biofeedback, no audio crudo.

### SessionManager
Gestiona inicio/finalización de sesiones, ejercicios, métricas, resultados y persistencia.

### Data
Aísla SQLite y repositorios de la UI y de los ejercicios.

## Modelos de audio

Usar modelos simples y claros.

AudioFeatures puede contener:
- timestamp;
- rms;
- energy;
- amplitude;
- duration;
- activity/voice state;
- f0 cuando exista;
- confidence cuando corresponda.

BiofeedbackResult puede contener:
- value;
- active;
- intensity;
- confidence;
- timestamp.

No mezclar en estos modelos lógica clínica, UI y persistencia.

## Captura de micrófono
Usar las APIs apropiadas de Godot para audio en tiempo real.

Verificar contra la versión instalada:
- AudioStreamMicrophone;
- AudioEffectCapture;
- buses de audio;
- buffers;
- muestras PCM;
- frecuencia de muestreo;
- canales.

No asumir que una API o propiedad existe. Verificar primero la documentación/API correspondiente a la versión del proyecto.

## Procesamiento
Trabajar sobre muestras PCM cuando sea necesario.

Priorizar:
- procesamiento por bloques;
- reutilización de buffers;
- evitar allocations innecesarias;
- procesamiento incremental;
- separación entre procesamiento y renderizado.

No recalcular continuamente la señal completa si sólo llegó un bloque nuevo.

No hacer procesamiento pesado dentro de _Process() si puede ejecutarse a una frecuencia independiente.

## RMS
Implementar inicialmente:

RMS = sqrt((1/N) * Σ(x_i²))

La implementación debe:
- aceptar buffers;
- manejar buffers vacíos;
- evitar problemas numéricos;
- ser testeable sin micrófono;
- no depender de la UI.

IMPORTANTE: RMS del micrófono es una medida acústica. No equivale directamente a flujo de aire u otra medida fisiológica sin calibración/validación.

## Normalización y biofeedback
Nunca conectar RMS directamente con movimiento visual.

Pipeline:

raw RMS
 ↓
preprocesamiento
 ↓
normalización
 ↓
threshold
 ↓
smoothing
 ↓
BiofeedbackResult
 ↓
ejercicio

Los parámetros deben ser configurables y no estar dispersos como números mágicos.

## Ejercicio: El Gran Globo
El paciente realiza un soplo sostenido y el globo responde al biofeedback.

Micrófono
 ↓
AudioCapture
 ↓
AudioProcessor
 ↓
FeatureExtractor
 ↓
BiofeedbackEngine
 ↓
BalloonExerciseController
 ↓
Globo

BalloonExerciseController recibe biofeedback y no conoce micrófono, PCM, RMS ni SQLite.

Debe poder probarse con valores simulados.

## Ejercicio: Vuelo del Pájaro
El paciente controla el movimiento del pájaro mediante la intensidad relativa de la señal.

Debe reutilizar el pipeline de audio existente.

No duplicar captura ni procesamiento para cada ejercicio.

## Sistema común de ejercicios
Crear una abstracción común sólo si existe comportamiento compartido real.

Los ejercicios deberían poder:
- iniciar;
- pausar;
- reiniciar;
- finalizar;
- recibir biofeedback;
- registrar métricas;
- generar resultados.

Evitar una clase gigante GameManager.

## Nodes de Godot
No convertir todas las clases C# en Node.

Usar Node/Node2D/Control/etc. cuando la clase necesite árbol de escenas, ciclo de vida o comunicación de Godot.

Las clases puras de cálculo, dominio o procesamiento deben permanecer independientes de Godot cuando sea razonable para facilitar testing.

## Signals
Usar Signals cuando ayuden al desacoplamiento.

Ejemplo:
AudioCapture → signal → AudioProcessor
BiofeedbackEngine → signal → ExerciseController
ExerciseController → signal → UI

No utilizar signals para todo. Si una llamada directa es más clara, usarla.

## Resources
Usar Godot Resource para configuración editable cuando aporte valor:
- thresholds;
- sensibilidad;
- duración;
- dificultad;
- objetivos;
- parámetros de biofeedback.

No usar Resources como reemplazo universal de clases de dominio.

## Configuración
Evitar valores mágicos.

Parámetros potencialmente configurables:
- sample rate;
- buffer size;
- ventana RMS;
- threshold;
- smoothing;
- sensibilidad;
- duración;
- objetivos.

Centralizar configuración cuando corresponda.

## Persistencia
El MVP es offline-first.

Usar SQLite y separar conceptualmente:
- Domain;
- Application;
- Infrastructure/Data.

Entidades potenciales:
- Patient;
- Exercise;
- Session;
- SessionResult;
- AcousticMetric.

Repositorios potenciales:
- PatientRepository;
- ExerciseRepository;
- SessionRepository;
- ResultRepository.

Nunca colocar SQL directamente en UI o ejercicios.

## Sesiones
Una sesión puede registrar:
- paciente;
- fecha/hora;
- ejercicio;
- duración;
- métricas;
- puntuación;
- resultado.

No asumir que una métrica acústica tiene significado clínico por sí misma.

## Arquitectura futura

Godot
 ↓
SQLite
 ↓
sincronización futura
 ↓
ASP.NET Core API
 ↓
PostgreSQL
 ↓
React + TypeScript

La sincronización NO forma parte del MVP. La aplicación debe funcionar sin backend.

## Testing
Priorizar tests para:
- RMS;
- energía;
- normalización;
- thresholds;
- smoothing;
- detección de actividad;
- conversión de features a biofeedback;
- puntuación;
- reglas de ejercicios.

Usar buffers artificiales:
- silencio;
- señal constante;
- seno;
- ruido controlado;
- diferentes amplitudes.

Los tests unitarios no deben depender de un micrófono físico.

## Rendimiento
Evitar en loops críticos:
- allocations innecesarias;
- LINQ;
- creación constante de objetos;
- conversiones innecesarias;
- procesamiento duplicado;
- acceso excesivo a UI.

Separar procesamiento acústico de renderizado visual.

## Errores y logging
Manejar explícitamente:
- micrófono inexistente;
- acceso/permiso al micrófono;
- dispositivo no disponible;
- buffer insuficiente;
- captura detenida;
- errores SQLite;
- datos inválidos;
- configuración incompleta.

No usar `catch { }`.

Usar logging apropiado de Godot, claro y útil. No imprimir muestras de audio continuamente.

## UI
La UI recibe resultados procesados.

La UI NO debe:
- leer PCM;
- calcular RMS;
- procesar audio;
- consultar SQLite continuamente.

Flujo:

Audio
 ↓
procesamiento
 ↓
BiofeedbackResult
 ↓
ExerciseController / UI

## Principios de diseño
Aplicar cuando corresponda:
- SOLID;
- separación de responsabilidades;
- bajo acoplamiento;
- alta cohesión;
- composición;
- interfaces donde aporten valor;
- dependency injection cuando sea razonable;
- código testeable;
- nombres descriptivos;
- métodos pequeños.

No aplicar patrones sólo por tener patrones. La simplicidad tiene prioridad.

## Evitar sobreingeniería
NO:
- microservicios;
- sistemas de eventos complejos;
- capas innecesarias;
- patrones GoF sin necesidad;
- abstracciones sin beneficio real;
- managers globales innecesarios;
- duplicación de modelos sin razón.

Antes de agregar una abstracción, identificar qué problema real resuelve.

## Dependencias
Antes de instalar una dependencia:
1. comprobar si es necesaria;
2. comprobar si Godot ya proporciona la funcionalidad;
3. comprobar compatibilidad;
4. explicar su propósito;
5. mantener el MVP con pocas dependencias.

No instalar paquetes automáticamente sin justificación.

## Desarrollo incremental
Orden recomendado:

1. Diagnóstico del entorno.
2. Configuración de Godot .NET.
3. Proyecto C# mínimo.
4. Estructura inicial.
5. Prueba de micrófono.
6. Captura PCM.
7. RMS.
8. Normalización.
9. Visualización de intensidad.
10. Prototipo del Globo.
11. Biofeedback formal.
12. Sistema común de ejercicios.
13. Sesiones.
14. SQLite.
15. UI.
16. Pájaro.
17. Tests.
18. Refactor y estabilización.

No implementar todo de una vez.

## Regla antes de modificar código
Antes de una modificación significativa:
1. inspeccionar el código existente;
2. identificar dependencias;
3. explicar brevemente la estrategia;
4. indicar archivos afectados;
5. detectar efectos secundarios;
6. implementar incrementalmente;
7. revisar errores/compilación.

No sobrescribir arquitectura existente sin analizarla. No borrar archivos sin justificarlo.

## Documentación
Documentar decisiones arquitectónicas importantes, especialmente:
- audio;
- captura de micrófono;
- procesamiento;
- normalización;
- biofeedback;
- persistencia;
- dependencias;
- decisiones clínicas/técnicas relevantes.

Documentar el “por qué”, no sólo el “qué”.

## Criterio clínico/técnico
Las señales del micrófono son señales acústicas.

No interpretar automáticamente una característica acústica como medición fisiológica.

Ejemplo:

RMS del micrófono ≠ flujo de aire medido directamente.

Cualquier interpretación clínica debe contar posteriormente con criterios, calibración o validación apropiados.

## Primer objetivo técnico
Antes de construir toda la aplicación, demostrar:

Micrófono
 ↓
Godot Audio
 ↓
PCM
 ↓
RMS
 ↓
normalización
 ↓
barra de intensidad
 ↓
globo

El POC debe comprobar:
- detección del micrófono;
- obtención de PCM;
- lectura de buffers;
- cálculo de RMS;
- respuesta a cambios de intensidad;
- normalización estable;
- visualización;
- reacción del globo.

Sólo después de validar este pipeline avanzar hacia la arquitectura completa.

## Primera tarea de Copilot
Al abrir el proyecto por primera vez:

NO implementar funcionalidades todavía.

Realizar únicamente un diagnóstico del entorno:
- versión de Godot;
- confirmar que es Godot .NET;
- versión del .NET SDK;
- compilador/C# disponible;
- extensiones relevantes de VS Code;
- Git;
- estructura del repositorio;
- proyecto Godot existente;
- compatibilidad entre versiones.

Presentar:
1. Estado actual.
2. Problemas encontrados.
3. Dependencias faltantes.
4. Compatibilidad.
5. Estructura recomendada.
6. Próximo paso.

NO instalar ni modificar nada hasta recibir confirmación.
