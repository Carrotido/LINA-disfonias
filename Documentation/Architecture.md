# Vocalis FonoPlay: arquitectura funcional

## Flujo principal

La aplicacion usa un flujo de datos desacoplado y adaptado a Godot .NET:

```text
Paciente
  -> AudioStreamMicrophone
  -> AudioCapture
  -> AudioProcessor
  -> FeatureExtractor
  -> BiofeedbackEngine
  -> ExerciseController
  -> interfaz y feedback
  -> SessionManager
  -> SQLite local
```

El microfono nunca modifica directamente un nodo visual ni decide la logica del ejercicio.

## Adaptacion a Godot

El documento funcional original menciona Unity en algunos puntos. En este proyecto se reemplaza por:

- `AudioStreamMicrophone` para enviar la entrada al bus de audio.
- `AudioEffectCapture` para leer muestras PCM en tiempo real.
- `AudioCapture` como adaptador de la API de Godot.
- `AudioProcessor` para preprocesamiento independiente de Godot.
- `FeatureExtractor` para RMS, energia, amplitud, duracion y actividad.
- `BiofeedbackEngine` para convertir caracteristicas en reglas genericas.
- `ExerciseController` para la mecanica concreta del ejercicio.

## Ejercicio del Gran Globo

El globo representa continuidad de soplo, no volumen absoluto. El RMS se usa como indicador acustico de actividad para cruzar umbrales configurables. Una vez activo:

- la duracion continua incrementa el progreso;
- los microcortes breves pueden tolerarse;
- una interrupcion prolongada reinicia la continuidad;
- el ejercicio no conoce PCM, captura ni SQLite.

Los umbrales y la duracion objetivo deben calibrarse con datos reales del dispositivo y validarse clinicamente. Una medida acustica no equivale automaticamente a flujo de aire fisiologico.

## Estado de implementacion

- Captura PCM: implementada con APIs de Godot 4.7 .NET.
- Preprocesamiento basico: eliminacion de componente DC y clamp de muestras.
- Caracteristicas iniciales: RMS, energia, amplitud, duracion y actividad.
- Biofeedback de soplo sostenido: implementado con umbral de activacion, liberacion y tolerancia de microcorte.
- Persistencia y sesiones: proximos modulos, manteniendo SQLite offline-first.
