using Godot;
using VocalisFonoPlay.Audio.Capture;
using VocalisFonoPlay.Audio.Features;
using VocalisFonoPlay.Audio.Processing;
using VocalisFonoPlay.Biofeedback;
using VocalisFonoPlay.Exercises.Balloon;

namespace VocalisFonoPlay.Core;

public partial class Main : Node2D
{
    private const string CaptureBusName = "Record";
    private const int SampleRate = 48000;
    private const int FramesPerRead = 512;

    // Clinical / adult design tokens. A future pediatric theme can reuse the
    // same layout and exercise logic while supplying a different token set.
    // Tokens extracted from the Stitch UI system.  They are kept here so the
    // generated controls share the same visual language as the references.
    private static readonly Color Surface = new("#ffffff");
    private static readonly Color Canvas = new("#f8f9ff");
    private static readonly Color SurfaceLow = new("#eff4ff");
    private static readonly Color SurfaceHigh = new("#dce9ff");
    private static readonly Color Primary = new("#006194");
    private static readonly Color PrimaryActive = new("#007bb9");
    private static readonly Color PrimaryFixed = new("#cce5ff");
    private static readonly Color OnSurface = new("#0b1c30");
    private static readonly Color OnSurfaceVariant = new("#3f4850");
    private static readonly Color OutlineVariant = new("#bfc7d2");
    private static readonly Color Success = new("#006c49");
    private static readonly Color Warning = new("#a36700");

    private Label? _label;
    private Label? _statusLabel;
    private ProgressBar? _intensityBar;
    private Panel? _balloon;
    private ProgressBar? _flowGauge;
    private Label? _currentDurationLabel;
    private Label? _currentStateLabel;
    private ProgressBar? _activeIntensityBar;
    private Label? _activeDurationLabel;
    private Label? _activeStateLabel;
    private Control? _navigationLayer;
    private Control? _menuView;
    private Control? _exerciseView;
    private Control? _contentView;
    private Control? _exerciseToolbar;
    private AudioStreamPlayer? _microphonePlayer;
    private AudioCapture? _audioCapture;
    private FeatureExtractor? _featureExtractor;
    private AudioProcessor? _audioProcessor;
    private BiofeedbackEngine? _biofeedbackEngine;
    private BalloonExerciseController? _balloonController;
    private double _elapsedSeconds;

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
        _statusLabel = GetNode<Label>("Status");
        _intensityBar = GetNode<ProgressBar>("Intensity");
        _balloon = GetNode<Panel>("Balloon");
        _flowGauge = GetNode<ProgressBar>("CatalogPanel/FlowGauge");
        _currentDurationLabel = GetNode<Label>("TelemetryPanel/CurrentDuration");
        _currentStateLabel = GetNode<Label>("TelemetryPanel/CurrentState");

        BuildApplicationShell();
        BuildExerciseToolbar();

        _label.Text = "Vocalis FonoPlay";
        _featureExtractor = new FeatureExtractor(SampleRate);
        _audioProcessor = new AudioProcessor();
        _biofeedbackEngine = new BiofeedbackEngine(
            activationThreshold: 0.08d,
            releaseThreshold: 0.05d,
            targetDurationSeconds: 3.0d,
            maximumSilenceGapSeconds: 0.15d);
        _balloonController = new BalloonExerciseController();
        _balloonController.Start();

        if (!ProjectSettings.GetSetting("audio/driver/enable_input", false).AsBool())
        {
            _statusLabel.Text = "La entrada de audio está desactivada en el proyecto";
            return;
        }

        int busIndex = AudioServer.GetBusIndex(CaptureBusName);
        if (busIndex < 0)
        {
            AudioServer.AddBus();
            busIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(busIndex, CaptureBusName);
        }

        var captureEffect = new AudioEffectCapture
        {
            BufferLength = 0.2f,
        };
        AudioServer.AddBusEffect(busIndex, captureEffect);
        _audioCapture = new AudioCapture(captureEffect);
        _audioCapture.Initialize();

        _microphonePlayer = new AudioStreamPlayer
        {
            Stream = new AudioStreamMicrophone(),
            Bus = CaptureBusName,
        };
        AddChild(_microphonePlayer);
        _microphonePlayer.Play();

        _statusLabel.Text = _audioCapture.StartCapture()
            ? "Micrófono activo. Mantén un soplo continuo para elevar el globo."
            : "No se pudo iniciar la captura de audio";

        ShowMenu();
    }

    private void BuildApplicationShell()
    {
        _navigationLayer = new Control
        {
            Name = "ApplicationShell",
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 20,
        };
        _navigationLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_navigationLayer);

        // The exercise scene is hidden while the clinical shell is active, so
        // the shell owns its own canvas instead of relying on a hidden layer.
        var shellBackground = new ColorRect
        {
            Name = "ClinicalCanvas",
            Color = Canvas,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        shellBackground.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _navigationLayer.AddChild(shellBackground);

        var header = new Panel
        {
            Position = Vector2.Zero,
            Size = new Vector2(1280, 56),
        };
        header.AddThemeStyleboxOverride("panel", CreatePanelStyle(Surface, 0));
        _navigationLayer.AddChild(header);

        var headerTitle = CreateLabel("Vocalis FonoPlay", 16, Primary);
        headerTitle.Position = new Vector2(34, 8);
        header.AddChild(headerTitle);
        var headerSubtitle = CreateLabel("Rehabilitación Vocal y Biofeedback", 8, OnSurfaceVariant);
        headerSubtitle.Position = new Vector2(35, 29);
        header.AddChild(headerSubtitle);

        AddTopNavigationButton(header, "Mi inicio", new Vector2(250, 12), ShowMenu);
        AddTopNavigationButton(header, "Ejercicios", new Vector2(332, 12), ShowExerciseLibrary);
        AddTopNavigationButton(header, "Mi progreso", new Vector2(430, 12), ShowResults);
        AddBadge(header, "● Micrófono listo", new Vector2(892, 15), new Vector2(112, 25), new Color("#e8f8ef"), Success);
        AddBadge(header, "Mateo R.  |  Nivel 4", new Vector2(1014, 15), new Vector2(126, 25), SurfaceLow, Primary);
        AddActionButton(header, "Modo profesional", new Vector2(1148, 15), new Vector2(110, 25), Primary, ShowProfessionalDashboard);

        _contentView = new Control
        {
            Name = "ContentView",
            ClipContents = true,
        };
        _contentView.Position = new Vector2(0, 56);
        _contentView.Size = new Vector2(1280, 664);
        _navigationLayer.AddChild(_contentView);

        _menuView = BuildMenuView();
        _contentView.AddChild(_menuView);
    }

    private Control BuildMenuView()
    {
        var view = new Control { Name = "MenuView" };
        var title = CreateLabel("¡Hola, Mateo! ☀", 30, OnSurface);
        title.Position = new Vector2(44, 28);
        view.AddChild(title);
        var subtitle = CreateLabel("Listo para continuar: llevas 15 minutos de práctica guiada.", 14, OnSurfaceVariant);
        subtitle.Position = new Vector2(46, 70);
        view.AddChild(subtitle);

        var recommendation = CreatePanel(new Vector2(44, 112), new Vector2(720, 216), new Color("#d8fff0"), 18);
        view.AddChild(recommendation);
        AddBadge(recommendation, "RECOMENDADO PARA HOY", new Vector2(22, 18), new Vector2(144, 22), Surface, Primary);
        AddText(recommendation, "El Gran Globo", 24, OnSurface, new Vector2(22, 54));
        AddText(recommendation, "Soplo sostenido y estabilidad laríngea. Mantén el globo\ncentrado durante una emisión continua y suave.", 13, OnSurfaceVariant, new Vector2(22, 91), new Vector2(420, 42));
        AddBadge(recommendation, "Biofeedback activo", new Vector2(22, 151), new Vector2(125, 25), Surface, Success);
        AddActionButton(recommendation, "Comenzar ahora  ›", new Vector2(514, 147), new Vector2(180, 42), Primary, ShowExerciseLibrary);

        var weekly = CreatePanel(new Vector2(790, 112), new Vector2(440, 216), Surface, 18);
        view.AddChild(weekly);
        AddText(weekly, "Racha semanal", 17, OnSurface, new Vector2(22, 20));
        AddBadge(weekly, "4 días", new Vector2(330, 18), new Vector2(80, 25), new Color("#fff2d8"), Warning);
        AddText(weekly, "●   ●   ●   ●   ○   ○   ○", 22, Primary, new Vector2(24, 74));
        AddText(weekly, "¡Vas muy bien! Te faltan 3 días para\ncompletar tu objetivo semanal.", 13, OnSurfaceVariant, new Vector2(24, 128));

        AddText(view, "Tus ejercicios frecuentes", 18, OnSurface, new Vector2(44, 370));
        AddText(view, "Selecciona un módulo para continuar con tu rehabilitación diaria.", 12, OnSurfaceVariant, new Vector2(44, 395));
        AddDashboardExercise(view, "El Gran Globo", "FLUJO AÉREO · Nivel 3", new Vector2(44, 426), Success, ShowExerciseLibrary);
        AddDashboardExercise(view, "Vuelo del Pájaro", "INTONACIÓN · Nivel 2", new Vector2(422, 426), Primary, null);
        AddDashboardExercise(view, "Ondas Musicales", "RESONANCIA · Nivel 1", new Vector2(800, 426), Warning, null);
        return view;
    }

    private void AddMenuCard(Control parent, string title, string description, Vector2 position, Color accent, Action action)
    {
        var card = new Button
        {
            Text = $"{title}\n\n{description}",
            Position = position,
            Size = new Vector2(290, 156),
            Alignment = HorizontalAlignment.Left,
        };
        card.AddThemeFontSizeOverride("font_size", 16);
        card.AddThemeColorOverride("font_color", new Color("#111c2d"));
        card.AddThemeStyleboxOverride("normal", CreatePanelStyle(Surface, 1));
        card.AddThemeStyleboxOverride("hover", CreatePanelStyle(accent.Lightened(0.82f), 2));
        card.Pressed += action;
        parent.AddChild(card);
    }

    private void AddNavigationButton(Panel sidebar, string text, string icon, float y, Action action)
    {
        var button = new Button
        {
            Text = $"  {icon}    {text}",
            Position = new Vector2(16, y),
            Size = new Vector2(200, 42),
            Alignment = HorizontalAlignment.Left,
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeColorOverride("font_color", new Color("#ecf1ff"));
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(new Color(0, 0, 0, 0), 0));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(new Color("#176875"), 8));
        button.Pressed += action;
        sidebar.AddChild(button);
    }

    private void ShowMenu()
    {
        if (_navigationLayer is not null)
        {
            _navigationLayer.Visible = true;
        }
        SetExerciseVisible(false);
        ClearSectionViews();
        if (_menuView is not null)
        {
            _menuView.Visible = true;
        }
    }

    private void ShowExerciseLibrary()
    {
        ShowScreen(BuildExerciseLibraryView());
    }

    private void ShowPreparation()
    {
        ShowScreen(BuildPreparationView());
    }

    private void ShowProfessionalDashboard()
    {
        ShowScreen(BuildProfessionalDashboardView());
    }

    private void ShowPatientProfile()
    {
        ShowScreen(BuildPatientProfileView());
    }

    private void ShowCalibration()
    {
        ShowScreen(BuildCalibrationView());
    }

    private void ShowBalloonExercise()
    {
        if (_menuView is not null)
        {
            _menuView.Visible = false;
        }

        ClearSectionViews();
        if (_navigationLayer is not null)
        {
            _navigationLayer.Visible = false;
        }
        SetExerciseVisible(true);
    }

    private void ShowPatients()
    {
        ShowScreen(BuildPatientsView());
    }

    private void ShowAgenda()
    {
        ShowScreen(BuildAgendaView());
    }

    private void ShowResults()
    {
        ShowScreen(BuildResultsView());
    }

    private void ShowScreen(Control screen)
    {
        if (_navigationLayer is not null)
        {
            _navigationLayer.Visible = true;
        }
        SetExerciseVisible(false);
        if (_menuView is not null)
        {
            _menuView.Visible = false;
        }

        ClearSectionViews();
        _contentView?.AddChild(screen);
    }

    private void ClearSectionViews()
    {
        if (_contentView is null)
        {
            return;
        }

        foreach (Node child in _contentView.GetChildren())
        {
            if (child == _menuView)
            {
                continue;
            }

            // QueueFree occurs at the end of the frame. Hide first so the
            // outgoing view cannot remain visible under the incoming one.
            if (child is CanvasItem canvasItem)
            {
                canvasItem.Visible = false;
            }

            child.QueueFree();
        }
    }

    private Control BuildExerciseLibraryView()
    {
        var view = CreateScreen("Catálogo de Ejercicios Terapéuticos", "Selecciona una actividad diseñada para tu fonoaudiología. Avanza a tu propio ritmo.");
        AddBadge(view, "PLAN CLÍNICO ASIGNADO · CAMILA VALENZUELA", new Vector2(44, 18), new Vector2(270, 22), new Color("#d8fff0"), Success);
        AddBadge(view, "Todo (6)", new Vector2(44, 112), new Vector2(72, 26), Primary, Colors.White);
        AddBadge(view, "Control de Soplo (2)", new Vector2(124, 112), new Vector2(130, 26), Surface, OnSurfaceVariant);
        AddBadge(view, "Estabilidad Vocal (2)", new Vector2(262, 112), new Vector2(138, 26), Surface, OnSurfaceVariant);
        AddBadge(view, "Buscar ejercicio...", new Vector2(978, 112), new Vector2(206, 26), Surface, OnSurfaceVariant);
        AddExerciseCard(view, "El Gran Globo", "FLUJO AÉREO", "Controla el ascenso constante del globo manteniendo un soplo suave y continuo.", new Vector2(44, 158), Primary, "Disponible", ShowPreparation);
        AddExerciseCard(view, "Vuelo del Pájaro", "INTONACIÓN", "Eleva y modula tu voz con una melodía de tono suave.", new Vector2(334, 158), Success, "Nivel 2", null);
        AddExerciseCard(view, "El Barco de Papel", "RESONANCIA", "Impulsa el barco con olas suaves de resonancia.", new Vector2(624, 158), Warning, "Nivel 3", null);
        AddExerciseCard(view, "Ondas Sonoras del Océano", "RESPIRACIÓN", "Mantén un ritmo respiratorio estable y relajado.", new Vector2(44, 404), new Color("#5d7fa7"), "Bloqueado", null);
        AddExerciseCard(view, "El Cohete Espacial", "PROYECCIÓN", "Aumenta la intensidad de forma progresiva.", new Vector2(334, 404), Primary, "Nivel 2", null);
        AddExerciseCard(view, "Laberinto de Resonancia", "ARTICULACIÓN", "Encuentra el recorrido con vocales sostenidas.", new Vector2(624, 404), Success, "Nivel 3", null);
        return view;
    }

    private Control BuildPreparationView()
    {
        var view = CreateScreen("Preparación: El Gran Globo", "Objetivo: Mantener un flujo de soplo suave y uniforme para guiar el globo hacia la zona de vuelo óptima.");
        AddBadge(view, "CONTROL DE FLUJO RESPIRATORIO", new Vector2(44, 18), new Vector2(212, 22), SurfaceHigh, Primary);
        AddBadge(view, "Duración estimada  3 rondas · 15s", new Vector2(966, 35), new Vector2(218, 36), new Color("#e8f8ef"), Success);

        var technique = CreatePanel(new Vector2(44, 122), new Vector2(510, 360), Surface, 18);
        view.AddChild(technique);
        AddText(technique, "ⓘ  Técnica y Postura", 17, OnSurface, new Vector2(22, 20));
        AddInstruction(technique, "1", "Ponte cómodo", "Espalda recta, hombros relajados y mentón neutro.", 70);
        AddInstruction(technique, "2", "Inspiración diafragmática", "Inhala pausadamente por la nariz expandiendo el abdomen.", 142);
        AddInstruction(technique, "3", "Soplo continuo y constante", "Exhala con suavidad y controla la presión del aire.", 214);
        AddBadge(technique, "Consejo: frunce suavemente los labios y mantén\nuna sensación de aire ligera y constante.", new Vector2(22, 290), new Vector2(462, 47), new Color("#fff2d8"), Warning);

        var mic = CreatePanel(new Vector2(580, 122), new Vector2(604, 360), Surface, 18);
        view.AddChild(mic);
        AddText(mic, "●  Prueba de Micrófono en Vivo", 17, OnSurface, new Vector2(22, 20));
        AddBadge(mic, "● Reactivo", new Vector2(466, 20), new Vector2(108, 22), new Color("#e8f8ef"), Success);
        AddText(mic, "Comprueba que el micrófono responde al soplo antes del juego.", 12, OnSurfaceVariant, new Vector2(22, 47));
        var meter = CreatePanel(new Vector2(42, 92), new Vector2(510, 190), SurfaceLow, 14);
        mic.AddChild(meter);
        AddText(meter, "♬", 44, Primary, new Vector2(126, 55));
        AddText(meter, "Lectura: ¡Flujo óptimo!", 17, Success, new Vector2(210, 75));
        AddText(meter, "Puedes sostener esta señal limpia.", 12, OnSurfaceVariant, new Vector2(210, 101));
        for (var i = 0; i < 8; i++)
        {
            var level = new ColorRect { Position = new Vector2(468, 35 + i * 17), Size = new Vector2(15, 10), Color = i < 5 ? new Color("#49dca1") : new Color("#f3d7d2") };
            meter.AddChild(level);
        }

        var start = CreatePanel(new Vector2(44, 510), new Vector2(1140, 112), Surface, 18);
        view.AddChild(start);
        AddText(start, "✓  Todo listo para despegar", 17, Success, new Vector2(22, 22));
        AddText(start, "Relájate, centra tu atención en el aire y disfruta el ejercicio.", 12, OnSurfaceVariant, new Vector2(22, 53));
        AddActionButton(start, "Estoy listo, ¡comenzar!  ›", new Vector2(832, 36), new Vector2(276, 42), Primary, ShowBalloonExercise);
        return view;
    }

    private Control BuildProfessionalDashboardView()
    {
        var view = CreateProfessionalScreen("Dra. Camila Valenzuela", "Fonoaudióloga · Centro Fonoaudiológico Metropolitano", "Dashboard Profesional");
        AddBadge(view, "RESUMEN DE HOY · MARTES 12 DE NOVIEMBRE", new Vector2(244, 94), new Vector2(275, 22), SurfaceHigh, Primary);
        AddText(view, "Agenda y seguimiento clínico en tiempo real.", 13, OnSurfaceVariant, new Vector2(244, 121));
        AddActionButton(view, "Nueva sesión  +", new Vector2(1030, 100), new Vector2(160, 34), Primary, ShowPatientProfile);

        AddProfessionalMetric(view, new Vector2(244, 158), "PACIENTES ACTIVOS", "12", "+2 esta semana", Primary);
        AddProfessionalMetric(view, new Vector2(466, 158), "ADHERENCIA", "88%", "Objetivo superado", Success);
        AddProfessionalMetric(view, new Vector2(688, 158), "PROGRESO MEDIO", "91.4%", "Mejora sostenida", PrimaryActive);
        AddProfessionalMetric(view, new Vector2(910, 158), "ALERTAS CLÍNICAS", "2", "Requieren revisión", new Color("#ba1a1a"));

        var agenda = CreatePanel(new Vector2(244, 260), new Vector2(570, 314), Surface, 16);
        view.AddChild(agenda);
        AddText(agenda, "Próximas Sesiones y Estado", 17, OnSurface, new Vector2(20, 18));
        AddText(agenda, "HOY · MARTES 12", 10, OnSurfaceVariant, new Vector2(20, 45));
        AddAppointment(agenda, 74, "10:00", "Mateo Retamal (9 años)", "El Gran Globo · control de soplo", "En sala", Primary, ShowPatientProfile);
        AddAppointment(agenda, 137, "11:00", "Sofía Morales (14 años)", "Vuelo del Pájaro · entonación", "Ver ficha", Success, ShowPatientProfile);
        AddAppointment(agenda, 200, "12:30", "Lucas Benítez (7 años)", "Evaluación inicial", "En espera", Warning, ShowPatientProfile);

        var alerts = CreatePanel(new Vector2(836, 260), new Vector2(354, 314), Surface, 16);
        view.AddChild(alerts);
        AddText(alerts, "Alertas Clínicas", 17, OnSurface, new Vector2(20, 18));
        AddBadge(alerts, "2 pendientes", new Vector2(230, 18), new Vector2(102, 22), new Color("#fff2d8"), Warning);
        AddText(alerts, "Mateo R. · Revisión de flujo", 13, OnSurface, new Vector2(20, 70));
        AddText(alerts, "Dos microcortes detectados en su última práctica.", 11, OnSurfaceVariant, new Vector2(20, 92), new Vector2(305, 32));
        AddActionButton(alerts, "Ver ficha", new Vector2(220, 115), new Vector2(112, 30), SurfaceLow, ShowPatientProfile, Primary);
        AddText(alerts, "Sofía M. · Objetivo semanal", 13, OnSurface, new Vector2(20, 165));
        AddText(alerts, "Le faltan 2 sesiones para completar su plan.", 11, OnSurfaceVariant, new Vector2(20, 187), new Vector2(305, 32));
        AddActionButton(alerts, "Calibrar audio", new Vector2(20, 247), new Vector2(145, 34), SurfaceLow, ShowCalibration, Primary);

        var trend = CreatePanel(new Vector2(244, 594), new Vector2(946, 54), new Color("#e5f7ff"), 14);
        view.AddChild(trend);
        AddText(trend, "▰  Tendencia terapéutica positiva: el promedio de continuidad de soplo aumentó un 8.4% esta semana.", 12, Primary, new Vector2(18, 17));
        return view;
    }

    private Control BuildPatientProfileView()
    {
        var view = CreateProfessionalScreen("Mateo Retamal", "Paciente · 9 años · Disfonía funcional", "Ficha del paciente");
        AddBadge(view, "PLAN ACTIVO · FLUJO AÉREO", new Vector2(244, 94), new Vector2(190, 22), new Color("#d8fff0"), Success);
        AddText(view, "Mateo Retamal", 25, OnSurface, new Vector2(244, 122));
        AddText(view, "9 años · Escolar · Diagnóstico: disfonía funcional", 13, OnSurfaceVariant, new Vector2(244, 154));
        AddActionButton(view, "Iniciar nueva sesión  ›", new Vector2(986, 122), new Vector2(204, 38), Primary, ShowExerciseLibrary);

        var profile = CreatePanel(new Vector2(244, 188), new Vector2(946, 90), Surface, 16);
        view.AddChild(profile);
        AddBadge(profile, "MR", new Vector2(20, 20), new Vector2(50, 50), Primary, Colors.White);
        AddText(profile, "Objetivo terapéutico actual", 11, OnSurfaceVariant, new Vector2(92, 16));
        AddText(profile, "Sostener una emisión continua y estable durante 15 segundos.", 15, OnSurface, new Vector2(92, 35));
        AddBadge(profile, "Nivel 4 · Explorador vocal", new Vector2(704, 28), new Vector2(210, 30), SurfaceLow, Primary);

        AddProfessionalMetric(view, new Vector2(244, 302), "MEJOR SOPLO", "14.5 s", "Récord de Mateo", Success);
        AddProfessionalMetric(view, new Vector2(466, 302), "CONTINUIDAD", "0.62", "Índice normalizado", Primary);
        AddProfessionalMetric(view, new Vector2(688, 302), "SESIONES", "24", "Este ciclo", PrimaryActive);
        AddProfessionalMetric(view, new Vector2(910, 302), "ADHERENCIA", "89%", "Muy buena", Success);

        var curve = CreatePanel(new Vector2(244, 404), new Vector2(610, 190), Surface, 16);
        view.AddChild(curve);
        AddText(curve, "Curva de Progreso y Control Respiratorio", 16, OnSurface, new Vector2(20, 18));
        AddText(curve, "Tendencia de estabilidad en las últimas seis sesiones.", 11, OnSurfaceVariant, new Vector2(20, 43));
        var chart = new ColorRect { Position = new Vector2(22, 76), Size = new Vector2(566, 76), Color = new Color("#e2f5ff") };
        curve.AddChild(chart);
        AddText(chart, "╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱╱", 20, Success, new Vector2(12, 27));
        AddBadge(curve, "Progreso terapéutico favorable", new Vector2(20, 160), new Vector2(230, 22), new Color("#e8f8ef"), Success);

        var recommendation = CreatePanel(new Vector2(876, 404), new Vector2(314, 190), Surface, 16);
        view.AddChild(recommendation);
        AddText(recommendation, "Recomendación terapéutica", 15, OnSurface, new Vector2(18, 18));
        AddText(recommendation, "Mantener el protocolo El Gran Globo y aumentar progresivamente la duración objetivo.", 11, OnSurfaceVariant, new Vector2(18, 51), new Vector2(276, 52));
        AddActionButton(recommendation, "Ver calibración", new Vector2(18, 134), new Vector2(154, 32), SurfaceLow, ShowCalibration, Primary);
        return view;
    }

    private Control BuildCalibrationView()
    {
        var view = CreateProfessionalScreen("Calibración Acústica & Configuración de Audio", "Ajusta la sensibilidad del micrófono y valida la señal antes de iniciar una sesión.", "Calibración");
        AddBadge(view, "SISTEMA DE AUDIO · MICRÓFONO CLÍNICO USB", new Vector2(244, 94), new Vector2(300, 22), SurfaceHigh, Primary);
        AddActionButton(view, "Guardar configuración", new Vector2(1000, 102), new Vector2(190, 34), Primary, ShowProfessionalDashboard);

        var monitor = CreatePanel(new Vector2(244, 146), new Vector2(632, 256), Surface, 16);
        view.AddChild(monitor);
        AddText(monitor, "⌁  Monitor Bioacústico en Tiempo Real", 17, OnSurface, new Vector2(20, 18));
        AddBadge(monitor, "● Señal estable", new Vector2(460, 18), new Vector2(150, 22), new Color("#e8f8ef"), Success);
        var graph = new ColorRect { Position = new Vector2(20, 62), Size = new Vector2(592, 112), Color = new Color("#0b3045") };
        monitor.AddChild(graph);
        AddText(graph, "╭──╮       ╭───╮        ╭──╮       ╭───╮", 26, new Color("#63d8e9"), new Vector2(40, 42));
        AddText(monitor, "RMS actual  0.08     ·     Umbral de activación  0.08     ·     Señal limpia", 11, OnSurfaceVariant, new Vector2(20, 194));
        AddBadge(monitor, "✓ Micrófono preparado para biofeedback", new Vector2(20, 220), new Vector2(300, 22), new Color("#d8fff0"), Success);

        var level = CreatePanel(new Vector2(900, 146), new Vector2(290, 256), Surface, 16);
        view.AddChild(level);
        AddText(level, "Nivel de señal", 14, OnSurface, new Vector2(18, 18));
        AddText(level, "31.8 dB", 28, Primary, new Vector2(18, 48));
        AddBadge(level, "Óptimo", new Vector2(190, 54), new Vector2(78, 22), new Color("#d8fff0"), Success);
        AddText(level, "▰▰▰▰▰▰▰▰▱▱", 27, Success, new Vector2(18, 98));
        AddText(level, "Rango recomendado: 25–45 dB\nSin saturación ni ruido relevante.", 11, OnSurfaceVariant, new Vector2(18, 150));

        AddCalibrationCard(view, "Umbrales & Filtros de Detección", "Ajusta la activación y liberación del biofeedback.", new Vector2(244, 426), "0.08 RMS", Primary);
        AddCalibrationCard(view, "Dinámica Temporal & Antirruido", "Configura la tolerancia de microcortes y suavizado.", new Vector2(564, 426), "0.15 s", Success);
        AddCalibrationCard(view, "Impacto de la Calibración", "Valores recomendados para el protocolo actual.", new Vector2(884, 426), "Perfil clínico", Warning);
        var safe = CreatePanel(new Vector2(244, 604), new Vector2(946, 44), new Color("#d8fff0"), 14);
        view.AddChild(safe);
        AddText(safe, "✓  Configuración validada. Los cambios solo afectan al biofeedback visual; requieren validación clínica para su interpretación.", 11, Success, new Vector2(16, 14));
        return view;
    }

    private Control BuildPatientsView()
    {
        var view = CreateScreen("Pacientes", "Directorio local y seguimiento terapéutico.");
        AddPatientRow(view, new Vector2(44, 142), "Lucía Fernández", "42 años · Disfonía funcional", "Sesión prevista hoy · 10:30", "Emisión sostenida", Success);
        AddPatientRow(view, new Vector2(44, 234), "Martín Rojas", "56 años · Rehabilitación vocal", "Última sesión: 08 sep.", "Intensidad y resistencia", PrimaryActive);
        AddPatientRow(view, new Vector2(44, 326), "Elena Suárez", "35 años · Fatiga vocal", "Seguimiento pendiente", "Coordinación fono-respiratoria", Warning);

        var followUp = CreatePanel(new Vector2(44, 444), new Vector2(900, 120));
        view.AddChild(followUp);
        AddText(followUp, "Seguimiento de Lucía Fernández", 16, OnSurface, new Vector2(20, 16));
        AddText(followUp, "Objetivo actual", 11, OnSurfaceVariant, new Vector2(20, 50));
        AddText(followUp, "Sostener la emisión durante 3.0 s", 14, Primary, new Vector2(20, 68));
        AddText(followUp, "Próximo control", 11, OnSurfaceVariant, new Vector2(470, 50));
        AddText(followUp, "Hoy · 10:30", 14, Primary, new Vector2(470, 68));
        return view;
    }

    private Control BuildAgendaView()
    {
        var view = CreateScreen("Agenda", "Jueves, 12 de septiembre · Consultorio local.");
        var schedule = CreatePanel(new Vector2(44, 142), new Vector2(900, 332));
        view.AddChild(schedule);
        AddText(schedule, "Agenda de hoy", 17, OnSurface, new Vector2(20, 18));
        AddScheduleRow(schedule, 64, "09:00", "Martín Rojas", "Seguimiento vocal", PrimaryActive);
        AddScheduleRow(schedule, 122, "10:30", "Lucía Fernández", "El Gran Globo · emisión sostenida", Success);
        AddScheduleRow(schedule, 180, "12:00", "Bloque clínico", "Notas y revisión de resultados", Warning);
        AddScheduleRow(schedule, 238, "15:30", "Elena Suárez", "Primera evaluación", Primary);
        AddText(schedule, "Los horarios son datos de demostración; Agenda se conectará a SQLite en la fase de datos.", 11, OnSurfaceVariant, new Vector2(20, 294), new Vector2(840, 18));
        return view;
    }

    private Control BuildResultsView()
    {
        var view = CreateScreen("Has dominado El Gran Globo", "Has completado las 3 rondas de control de soplo con gran estabilidad y control respiratorio.");
        AddBadge(view, "✓ Ejecución serena, Mateo", new Vector2(44, 18), new Vector2(168, 24), new Color("#d8fff0"), Success);
        var score = CreatePanel(new Vector2(916, 26), new Vector2(268, 126), new Color("#e5fff3"), 18);
        view.AddChild(score);
        AddText(score, "RENDIMIENTO GENERAL", 10, OnSurfaceVariant, new Vector2(24, 18));
        AddText(score, "94 /100", 30, Primary, new Vector2(24, 48));
        AddBadge(score, "✓ Progreso óptimo", new Vector2(24, 91), new Vector2(145, 22), Surface, Success);

        var chart = CreatePanel(new Vector2(44, 178), new Vector2(820, 226), Surface, 18);
        view.AddChild(chart);
        AddText(chart, "Curva de Flujo Aéreo y Sostén", 17, OnSurface, new Vector2(22, 18));
        AddText(chart, "Tu patrón de emisión se mantuvo estable durante toda la sesión.", 12, OnSurfaceVariant, new Vector2(22, 44));
        var graph = new ColorRect { Position = new Vector2(24, 80), Size = new Vector2(770, 100), Color = new Color("#c8f3f4") };
        chart.AddChild(graph);
        AddText(graph, "─────────────────╭───╮────╭───╮────╲╱────╭───╮────────", 18, Primary, new Vector2(14, 35));
        AddBadge(chart, "✓ Comparación con la sesión anterior     +18% de estabilidad sostenida", new Vector2(22, 190), new Vector2(774, 22), new Color("#e8f8ef"), Success);
        AddMetricCard(view, new Vector2(44, 426), "Tiempo en zona óptima", "42 s", "de 45 s totales", Primary);
        AddMetricCard(view, new Vector2(312, 426), "Soplo continuo más largo", "14.5 s", "¡Nuevo récord personal!", Warning);
        AddMetricCard(view, new Vector2(580, 426), "Estabilidad", "Muy estable", "Variaciones suaves y controladas", Success);
        var note = CreatePanel(new Vector2(890, 178), new Vector2(294, 282), Surface, 18);
        view.AddChild(note);
        AddText(note, "Comentario terapéutico", 16, OnSurface, new Vector2(20, 18));
        AddText(note, "“La capacidad de mantener un flujo\nconstante es impresionante. Hoy\nlograste una emisión muy estable.”", 12, OnSurfaceVariant, new Vector2(20, 60), new Vector2(250, 70));
        AddText(note, "HITOS DE LA SESIÓN", 10, OnSurfaceVariant, new Vector2(20, 155));
        AddText(note, "✓  Aumento del soplo óptico\n✓  Respiración y flujo estables\n✓  Control de volumen", 12, Success, new Vector2(20, 180));
        var actions = CreatePanel(new Vector2(44, 570), new Vector2(1140, 54), Surface, 18);
        view.AddChild(actions);
        AddActionButton(actions, "↻ Repetir ejercicio", new Vector2(16, 8), new Vector2(170, 37), SurfaceLow, ShowPreparation, OnSurface);
        AddActionButton(actions, "Siguiente: Vuelo del Pájaro  ›", new Vector2(846, 8), new Vector2(278, 37), Success, ShowExerciseLibrary);
        return view;
    }

    private void ShowSection(string title, string heading, string description)
    {
        SetExerciseVisible(false);
        if (_menuView is not null)
        {
            _menuView.Visible = false;
        }
        ClearSectionViews();
        if (_contentView is null)
        {
            return;
        }

        var section = new Control { Name = "SectionView" };
        var headingLabel = CreateLabel(heading, 30, new Color("#111c2d"));
        headingLabel.Position = new Vector2(44, 44);
        section.AddChild(headingLabel);
        var descriptionLabel = CreateLabel(description, 15, new Color("#3e494a"));
        descriptionLabel.Position = new Vector2(46, 92);
        section.AddChild(descriptionLabel);
        var notice = new Panel { Position = new Vector2(44, 156), Size = new Vector2(720, 150) };
        notice.AddThemeStyleboxOverride("panel", CreatePanelStyle(Surface, 1));
        section.AddChild(notice);
        var noticeText = CreateLabel($"{title}\n\nEsta sección está preparada para conectarse con los modelos de dominio y SQLite local.", 16, new Color("#0b5c66"));
        noticeText.Position = new Vector2(24, 24);
        notice.AddChild(noticeText);
        _contentView.AddChild(section);
    }

    private void SetExerciseVisible(bool visible)
    {
        foreach (Node child in GetChildren())
        {
            if (child == _navigationLayer)
            {
                continue;
            }

            if (child is CanvasItem canvasItem)
            {
                if (child != _exerciseToolbar && child.Name.ToString() is "Header" or "Label" or "Context" or "CatalogPanel" or "TelemetryPanel" or "ControlPanel" or "Intensity" or "Status")
                {
                    canvasItem.Visible = false;
                    continue;
                }

                canvasItem.Visible = visible;
            }
        }
    }

    private void BuildExerciseToolbar()
    {
        // Keep the existing stage and animated balloon, but hide the legacy
        // panels so they cannot overlap the Stitch-aligned active exercise.
        foreach (var path in new[] { "Header", "Label", "Context", "CatalogPanel", "TelemetryPanel", "ControlPanel", "Intensity", "Status" })
        {
            var legacyNode = GetNodeOrNull<CanvasItem>(path);
            if (legacyNode is not null)
            {
                legacyNode.Visible = false;
            }
        }

        _exerciseToolbar = new Control
        {
            Name = "ExerciseToolbar",
            ZIndex = 10,
        };
        AddChild(_exerciseToolbar);

        var header = CreatePanel(Vector2.Zero, new Vector2(1280, 72), Surface, 0);
        _exerciseToolbar.AddChild(header);
        AddText(header, "Vocalis FonoPlay", 17, Primary, new Vector2(30, 12));
        AddText(header, "Rehabilitación Vocal y Biofeedback", 9, OnSurfaceVariant, new Vector2(31, 36));
        AddBadge(header, "Mi inicio", new Vector2(270, 20), new Vector2(70, 28), SurfaceLow, OnSurfaceVariant);
        AddBadge(header, "Ejercicios", new Vector2(346, 20), new Vector2(84, 28), Primary, Colors.White);
        AddBadge(header, "● Micrófono listo", new Vector2(890, 22), new Vector2(116, 24), new Color("#e8f8ef"), Success);
        AddBadge(header, "Mateo R. · Nivel 4", new Vector2(1016, 22), new Vector2(132, 24), SurfaceLow, Primary);

        var context = CreatePanel(new Vector2(16, 88), new Vector2(1248, 64), Surface, 18);
        _exerciseToolbar.AddChild(context);
        AddText(context, "◌  El Gran Globo", 17, OnSurface, new Vector2(20, 12));
        AddText(context, "Entrenamiento de soplo continuo", 10, OnSurfaceVariant, new Vector2(23, 35));
        AddBadge(context, "Ronda 2 de 3 · 00:11", new Vector2(520, 18), new Vector2(180, 27), SurfaceLow, Primary);
        AddBadge(context, "Puntos 450", new Vector2(875, 18), new Vector2(112, 27), new Color("#fff2d8"), Warning);
        AddActionButton(context, "Finalizar y ver resultado  ›", new Vector2(1010, 13), new Vector2(216, 38), Success, ShowResults);

        var intensity = CreatePanel(new Vector2(20, 180), new Vector2(220, 470), Surface, 18);
        _exerciseToolbar.AddChild(intensity);
        AddText(intensity, "INTENSIDAD", 11, OnSurfaceVariant, new Vector2(18, 18));
        AddText(intensity, "del soplo", 10, OnSurfaceVariant, new Vector2(18, 34));
        AddBadge(intensity, "Zona óptima", new Vector2(18, 72), new Vector2(116, 24), new Color("#d8fff0"), Success);
        _activeIntensityBar = new ProgressBar
        {
            Position = new Vector2(78, 116),
            Size = new Vector2(62, 230),
            MaxValue = 100d,
            ShowPercentage = false,
            FillMode = (int)ProgressBar.FillModeEnum.BottomToTop,
        };
        _activeIntensityBar.AddThemeStyleboxOverride("background", CreatePanelStyle(SurfaceHigh, 14));
        _activeIntensityBar.AddThemeStyleboxOverride("fill", CreatePanelStyle(new Color("#49dca1"), 14));
        intensity.AddChild(_activeIntensityBar);
        AddText(intensity, "Suave", 10, OnSurfaceVariant, new Vector2(82, 362));
        AddBadge(intensity, "Mantén el aire\nconstante", new Vector2(18, 402), new Vector2(184, 45), SurfaceLow, Primary);

        var session = CreatePanel(new Vector2(916, 180), new Vector2(344, 278), Surface, 18);
        _exerciseToolbar.AddChild(session);
        AddText(session, "SESIÓN", 11, OnSurfaceVariant, new Vector2(20, 20));
        AddBadge(session, "Ronda 2", new Vector2(244, 16), new Vector2(78, 24), SurfaceLow, Primary);
        AddText(session, "Emisión sostenida", 11, OnSurfaceVariant, new Vector2(20, 65));
        _activeDurationLabel = AddText(session, "0.0 s", 31, Primary, new Vector2(20, 87));
        _activeStateLabel = AddText(session, "En espera", 11, Success, new Vector2(190, 105));
        AddText(session, "Objetivo de la ronda", 11, OnSurfaceVariant, new Vector2(20, 151));
        AddText(session, "Mantener el globo dentro de la\nzona de vuelo óptima.", 12, OnSurface, new Vector2(20, 174), new Vector2(260, 40));
        AddBadge(session, "✓ Señal estable y calibrada", new Vector2(20, 232), new Vector2(220, 23), new Color("#e8f8ef"), Success);

        var footer = CreatePanel(new Vector2(260, 666), new Vector2(640, 38), Surface, 14);
        _exerciseToolbar.AddChild(footer);
        AddText(footer, "✓ Estabilidad de flujo: excelente", 11, Success, new Vector2(18, 11));
        AddText(footer, "Entrada acústica: micrófono calibrado", 10, OnSurfaceVariant, new Vector2(340, 12));
        _exerciseToolbar.Visible = false;
    }

    private Control CreateProfessionalScreen(string title, string subtitle, string activeSection)
    {
        var view = new Control { Name = "ProfessionalView" };
        var sidebar = new Panel { Position = Vector2.Zero, Size = new Vector2(210, 664) };
        sidebar.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color("#f1f6ff"), 0));
        view.AddChild(sidebar);
        AddText(sidebar, "MODO PROFESIONAL", 10, Primary, new Vector2(20, 20));
        AddProfessionalNavigation(sidebar, "Resumen clínico", 56, activeSection == "Dashboard Profesional", ShowProfessionalDashboard);
        AddProfessionalNavigation(sidebar, "Pacientes & fichas", 96, activeSection == "Ficha del paciente", ShowPatientProfile);
        AddProfessionalNavigation(sidebar, "Calibración acústica", 136, activeSection == "Calibración", ShowCalibration);
        AddProfessionalNavigation(sidebar, "Volver al modo paciente", 196, false, ShowMenu);
        var therapist = CreatePanel(new Vector2(16, 560), new Vector2(178, 74), Surface, 12);
        sidebar.AddChild(therapist);
        AddText(therapist, "Dra. Camila Valenzuela", 11, OnSurface, new Vector2(12, 12));
        AddText(therapist, "Fonoaudióloga · sesión local", 9, OnSurfaceVariant, new Vector2(12, 34));

        AddText(view, title, 25, OnSurface, new Vector2(244, 28));
        AddText(view, subtitle, 12, OnSurfaceVariant, new Vector2(244, 62), new Vector2(690, 22));
        return view;
    }

    private void AddProfessionalNavigation(Control parent, string text, float y, bool selected, Action action)
    {
        var button = new Button { Text = text, Position = new Vector2(14, y), Size = new Vector2(182, 32), Alignment = HorizontalAlignment.Left };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", selected ? Colors.White : OnSurfaceVariant);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(selected ? Primary : new Color(0, 0, 0, 0), 10));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(selected ? PrimaryActive : SurfaceHigh, 10));
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void AddProfessionalMetric(Control parent, Vector2 position, string label, string value, string detail, Color accent)
    {
        var card = CreatePanel(position, new Vector2(202, 80), Surface, 14);
        parent.AddChild(card);
        AddText(card, label, 9, OnSurfaceVariant, new Vector2(14, 11));
        AddText(card, value, 23, accent, new Vector2(14, 28));
        AddText(card, detail, 9, accent, new Vector2(85, 47), new Vector2(103, 18));
    }

    private void AddAppointment(Control parent, float y, string time, string name, string detail, string actionText, Color accent, Action action)
    {
        var row = new Panel { Position = new Vector2(16, y), Size = new Vector2(538, 55) };
        row.AddThemeStyleboxOverride("panel", CreatePanelStyle(SurfaceLow, 10));
        parent.AddChild(row);
        AddText(row, time, 12, accent, new Vector2(12, 18));
        AddText(row, name, 12, OnSurface, new Vector2(70, 9));
        AddText(row, detail, 10, OnSurfaceVariant, new Vector2(70, 28), new Vector2(250, 18));
        AddActionButton(row, actionText, new Vector2(418, 13), new Vector2(106, 29), Surface, action, accent);
    }

    private void AddCalibrationCard(Control parent, string title, string description, Vector2 position, string value, Color accent)
    {
        var card = CreatePanel(position, new Vector2(294, 154), Surface, 16);
        parent.AddChild(card);
        AddText(card, title, 13, OnSurface, new Vector2(16, 16), new Vector2(260, 20));
        AddText(card, description, 10, OnSurfaceVariant, new Vector2(16, 42), new Vector2(258, 32));
        AddText(card, value, 18, accent, new Vector2(16, 90));
        AddBadge(card, "Configurado", new Vector2(164, 96), new Vector2(110, 22), SurfaceLow, accent);
    }

    private Control CreateScreen(string title, string subtitle)
    {
        var view = new Control { Name = "SectionView" };
        AddText(view, title, 30, OnSurface, new Vector2(42, 38));
        AddText(view, subtitle, 14, OnSurfaceVariant, new Vector2(44, 82), new Vector2(880, 24));
        return view;
    }

    private Panel CreatePanel(Vector2 position, Vector2 size)
    {
        return CreatePanel(position, size, Surface, 12);
    }

    private Panel CreatePanel(Vector2 position, Vector2 size, Color color, int radius)
    {
        var panel = new Panel { Position = position, Size = size };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(color, radius));
        return panel;
    }

    private void AddTopNavigationButton(Control parent, string text, Vector2 position, Action action)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = new Vector2(78, 31),
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", Primary);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(SurfaceLow, 14));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(PrimaryFixed, 14));
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void AddBadge(Control parent, string text, Vector2 position, Vector2 size, Color background, Color foreground)
    {
        var badge = new Panel { Position = position, Size = size };
        badge.AddThemeStyleboxOverride("panel", CreatePanelStyle(background, 12));
        parent.AddChild(badge);
        var label = CreateLabel(text, 10, foreground);
        label.Position = new Vector2(8, 4);
        label.Size = size - new Vector2(16, 8);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        badge.AddChild(label);
    }

    private void AddActionButton(Control parent, string text, Vector2 position, Vector2 size, Color background, Action action, Color? foreground = null)
    {
        var button = new Button { Text = text, Position = position, Size = size };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.AddThemeColorOverride("font_color", foreground ?? Colors.White);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(background, 12));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(background.Lightened(0.08f), 12));
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void AddDashboardExercise(Control parent, string title, string category, Vector2 position, Color accent, Action? action)
    {
        var card = CreatePanel(position, new Vector2(348, 166), Surface, 16);
        parent.AddChild(card);
        var icon = new Panel { Position = new Vector2(18, 18), Size = new Vector2(42, 42) };
        icon.AddThemeStyleboxOverride("panel", CreatePanelStyle(accent.Lightened(0.7f), 14));
        card.AddChild(icon);
        AddText(icon, "◌", 25, accent, new Vector2(9, 3));
        AddText(card, category, 9, accent, new Vector2(18, 74));
        AddText(card, title, 16, OnSurface, new Vector2(18, 92));
        AddText(card, "Sesión breve y personalizada", 11, OnSurfaceVariant, new Vector2(18, 117));
        if (action is not null)
        {
            AddActionButton(card, "Practicar  ›", new Vector2(238, 116), new Vector2(92, 32), SurfaceLow, action, Primary);
        }
    }

    private void AddInstruction(Control parent, string number, string title, string description, float y)
    {
        var item = new Panel { Position = new Vector2(20, y), Size = new Vector2(470, 58) };
        item.AddThemeStyleboxOverride("panel", CreatePanelStyle(SurfaceLow, 10));
        parent.AddChild(item);
        AddBadge(item, number, new Vector2(10, 16), new Vector2(26, 26), Surface, Primary);
        AddText(item, title, 13, OnSurface, new Vector2(48, 9));
        AddText(item, description, 10, OnSurfaceVariant, new Vector2(48, 28), new Vector2(400, 21));
    }

    private Label AddText(Control parent, string text, int fontSize, Color color, Vector2 position, Vector2? size = null)
    {
        var label = CreateLabel(text, fontSize, color);
        label.Position = position;
        if (size.HasValue)
        {
            label.Size = size.Value;
        }

        parent.AddChild(label);
        return label;
    }

    private void AddExerciseCard(Control parent, string title, string category, string description, Vector2 position, Color accent, string status, Action? action)
    {
        var card = CreatePanel(position, new Vector2(270, 218), Surface, 16);
        parent.AddChild(card);
        var accentBar = new ColorRect { Position = new Vector2(0, 0), Size = new Vector2(6, 218), Color = accent, MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddChild(accentBar);
        AddText(card, category.ToUpperInvariant(), 11, accent, new Vector2(22, 20));
        AddText(card, title, 19, OnSurface, new Vector2(22, 42));
        AddText(card, description, 12, OnSurfaceVariant, new Vector2(22, 76), new Vector2(222, 52));
        AddText(card, status.ToUpperInvariant(), 11, action is null ? OnSurfaceVariant : Success, new Vector2(22, 145));

        var button = new Button
        {
            Text = action is null ? "No disponible" : "Iniciar sesión",
            Position = new Vector2(140, 170),
            Size = new Vector2(108, 32),
            Disabled = action is null,
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", action is null ? OnSurfaceVariant : Colors.White);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(action is null ? new Color("#e7eeff") : accent, 1));
        if (action is not null)
        {
            button.Pressed += action;
        }

        card.AddChild(button);
    }

    private void AddPatientRow(Control parent, Vector2 position, string name, string details, string appointment, string goal, Color accent)
    {
        var row = CreatePanel(position, new Vector2(900, 76));
        parent.AddChild(row);
        var marker = new ColorRect { Position = new Vector2(0, 0), Size = new Vector2(5, 76), Color = accent, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(marker);
        AddText(row, name, 16, OnSurface, new Vector2(20, 13));
        AddText(row, details, 12, OnSurfaceVariant, new Vector2(20, 38));
        AddText(row, appointment, 12, accent, new Vector2(360, 17), new Vector2(210, 20));
        AddText(row, goal, 12, OnSurfaceVariant, new Vector2(610, 17), new Vector2(260, 36));
    }

    private void AddScheduleRow(Control parent, float y, string time, string patient, string protocol, Color accent)
    {
        var divider = new ColorRect { Position = new Vector2(20, y + 48), Size = new Vector2(860, 1), Color = OutlineVariant, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(divider);
        AddText(parent, time, 14, Primary, new Vector2(20, y + 12));
        AddText(parent, patient, 14, OnSurface, new Vector2(116, y + 12));
        AddText(parent, protocol, 12, OnSurfaceVariant, new Vector2(350, y + 13), new Vector2(380, 20));
        var dot = new ColorRect { Position = new Vector2(842, y + 16), Size = new Vector2(12, 12), Color = accent, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(dot);
    }

    private void AddMetricCard(Control parent, Vector2 position, string label, string value, string detail, Color accent)
    {
        var card = CreatePanel(position, new Vector2(250, 126), Surface, 16);
        parent.AddChild(card);
        AddText(card, label.ToUpperInvariant(), 10, OnSurfaceVariant, new Vector2(18, 14), new Vector2(214, 18));
        AddText(card, value, 25, accent, new Vector2(18, 38));
        AddText(card, detail, 11, OnSurfaceVariant, new Vector2(18, 84), new Vector2(214, 30));
    }

    private Label CreateLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private StyleBoxFlat CreatePanelStyle(Color color, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = OutlineVariant,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ShadowColor = new Color(0.067f, 0.11f, 0.176f, 0.05f),
            ShadowSize = 3,
            ShadowOffset = new Vector2(0, 1),
        };
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
        return style;
    }

    private StyleBoxFlat CreateSidebarStyle()
    {
        return new StyleBoxFlat { BgColor = Primary };
    }

    public override void _Process(double delta)
    {
        if (_audioCapture is null || _audioProcessor is null || _featureExtractor is null || _biofeedbackEngine is null || _balloonController is null)
        {
            return;
        }

        _elapsedSeconds += delta;
        if (!_audioCapture.TryGetBuffer(FramesPerRead, out Vector2[] stereoBuffer))
        {
            return;
        }

        var monoSamples = new float[stereoBuffer.Length];
        for (int i = 0; i < stereoBuffer.Length; i++)
        {
            Vector2 frame = stereoBuffer[i];
            monoSamples[i] = (frame.X + frame.Y) * 0.5f;
        }

        float[] processedSamples = _audioProcessor.Process(monoSamples);
        AudioFeatures features = _featureExtractor.Extract(processedSamples, _elapsedSeconds);
        BiofeedbackResult feedback = _biofeedbackEngine.Evaluate(features.Rms, features.Timestamp);
        _balloonController.ApplyBiofeedback(feedback);

        if (_intensityBar is not null)
        {
            _intensityBar.Value = feedback.Value * 100d;
        }

        if (_activeIntensityBar is not null)
        {
            _activeIntensityBar.Value = feedback.Value * 100d;
        }

        if (_flowGauge is not null)
        {
            _flowGauge.Value = feedback.Value * 100d;
        }

        if (_currentDurationLabel is not null)
        {
            _currentDurationLabel.Text = $"{feedback.ActiveDuration:0.0} s";
        }

        if (_activeDurationLabel is not null)
        {
            _activeDurationLabel.Text = $"{feedback.ActiveDuration:0.0} s";
        }

        if (_currentStateLabel is not null)
        {
            _currentStateLabel.Text = feedback.Active ? "Soplo activo" : "En espera";
        }

        if (_activeStateLabel is not null)
        {
            _activeStateLabel.Text = feedback.Active ? "Soplo activo" : "En espera";
        }

        if (_balloon is not null)
        {
            float height = (float)(feedback.Value * 260d);
            _balloon.Position = new Vector2(_balloon.Position.X, 390f - height);
            _balloon.Scale = Vector2.One * (0.8f + (float)feedback.Value * 0.4f);
        }
    }
}
