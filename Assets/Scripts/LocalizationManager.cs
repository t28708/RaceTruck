using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage
{
    Russian = 0,
    English = 1,
    French = 2,
    Spanish = 3
}

/// <summary>
/// Centralized In-Game Localization Manager.
/// Supports Russian (Русский), English, French (Français), and Spanish (Español).
/// Persists chosen language in PlayerPrefs and triggers OnLanguageChanged event.
/// </summary>
public static class LocalizationManager
{
    private const string PrefKey = "GameLanguage";

    public static event Action OnLanguageChanged;

    public static GameLanguage CurrentLanguage
    {
        get => currentLanguage;
        set
        {
            if (currentLanguage != value || !isInitialized)
            {
                currentLanguage = value;
                isInitialized = true;
                PlayerPrefs.SetInt(PrefKey, (int)currentLanguage);
                PlayerPrefs.Save();
                OnLanguageChanged?.Invoke();
            }
        }
    }

    private static GameLanguage currentLanguage = GameLanguage.Russian;
    private static bool isInitialized = false;

    static LocalizationManager()
    {
        LoadSavedLanguage();
    }

    private static void LoadSavedLanguage()
    {
        int saved = PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.Russian);
        if (Enum.IsDefined(typeof(GameLanguage), saved))
        {
            currentLanguage = (GameLanguage)saved;
        }
        else
        {
            currentLanguage = GameLanguage.Russian;
        }
        isInitialized = true;
    }

    public static GameLanguage CycleLanguage()
    {
        int count = Enum.GetValues(typeof(GameLanguage)).Length;
        int next = ((int)CurrentLanguage + 1) % count;
        CurrentLanguage = (GameLanguage)next;
        return CurrentLanguage;
    }

    public static string GetLanguageName(GameLanguage lang)
    {
        switch (lang)
        {
            case GameLanguage.Russian: return "РУССКИЙ";
            case GameLanguage.English: return "ENGLISH";
            case GameLanguage.French:  return "FRANÇAIS";
            case GameLanguage.Spanish: return "ESPAÑOL";
            default: return "РУССКИЙ";
        }
    }

    public static string GetLanguageButtonText()
    {
        string prefix;
        switch (CurrentLanguage)
        {
            case GameLanguage.Russian: prefix = "🌐 ЯЗЫК: "; break;
            case GameLanguage.English: prefix = "🌐 LANGUAGE: "; break;
            case GameLanguage.French:  prefix = "🌐 LANGUE: "; break;
            case GameLanguage.Spanish: prefix = "🌐 IDIOMA: "; break;
            default: prefix = "🌐 ЯЗЫК: "; break;
        }
        return prefix + GetLanguageName(CurrentLanguage);
    }

    public static string GetPedalSideButtonText(bool isLeft)
    {
        return isLeft ? Get("PEDALS_LEFT") : Get("PEDALS_RIGHT");
    }

    public static string GetAutoCenterButtonText(bool isEnabled)
    {
        return isEnabled ? Get("STEERING_AUTOCENTER_ON") : Get("STEERING_AUTOCENTER_OFF");
    }

    public static string GetSoundButtonText(bool isEnabled)
    {
        return isEnabled ? Get("SOUND_ON") : Get("SOUND_OFF");
    }

    public static string GetProgressText(int completed, int total)
    {
        if (total <= 0)
        {
            return Get("PROGRESS_ZERO_MAPS");
        }
        if (completed >= total)
        {
            return string.Format(Get("PROGRESS_ALL_DONE"), total);
        }
        if (completed > 0)
        {
            int percent = Mathf.RoundToInt((float)completed / total * 100f);
            return string.Format(Get("PROGRESS_COMPLETED"), completed, total, percent);
        }
        return string.Format(Get("PROGRESS_NOT_STARTED"), total);
    }

    private static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
    {
        // ========================
        // In-Game Menu
        // ========================
        { "BTN_MENU", new[] { "☰ МЕНЮ", "☰ MENU", "☰ MENU", "☰ MENÚ" } },
        { "MENU_TITLE", new[] { "⏸ МЕНЮ ИГРЫ", "⏸ GAME MENU", "⏸ MENU DU JEU", "⏸ MENÚ DEL JUEGO" } },
        { "MENU_CONTROLS_PREFIX", new[] { "🎮 УПРАВЛЕНИЕ: ", "🎮 CONTROLS: ", "🎮 CONTRÔLES: ", "🎮 CONTROLES: " } },
        { "PEDALS_LEFT", new[] { "🦶 ПЕДАЛИ: СЛЕВА", "🦶 PEDALS: LEFT", "🦶 PÉDALES: GAUCHE", "🦶 PEDALES: IZQUIERDA" } },
        { "PEDALS_RIGHT", new[] { "🦶 ПЕДАЛИ: СПРАВА", "🦶 PEDALS: RIGHT", "🦶 PÉDALES: DROITE", "🦶 PEDALES: DERECHA" } },
        { "STEERING_AUTOCENTER_ON", new[] { "🔄 ВОЗВРАТ РУЛЯ: ВКЛ", "🔄 AUTO-CENTER: ON", "🔄 RETOUR AU CENTRE: OUI", "🔄 AUTOCENTRADO: SÍ" } },
        { "STEERING_AUTOCENTER_OFF", new[] { "🔄 ВОЗВРАТ РУЛЯ: ВЫКЛ", "🔄 AUTO-CENTER: OFF", "🔄 RETOUR AU CENTRE: NON", "🔄 AUTOCENTRADO: NO" } },
        { "SOUND_ON", new[] { "🔊 ЗВУК: ВКЛ", "🔊 SOUND: ON", "🔊 SON: OUI", "🔊 SONIDO: SÍ" } },
        { "SOUND_OFF", new[] { "🔇 ЗВУК: ВЫКЛ", "🔇 SOUND: OFF", "🔇 SON: NON", "🔇 SONIDO: NO" } },
        { "MENU_RESTART", new[] { "🔄 ПЕРЕЗАПУСТИТЬ КАРТУ", "🔄 RESTART MAP", "🔄 REDÉMARRER LA CARTE", "🔄 REINICIAR MAPA" } },
        { "MENU_MAIN", new[] { "🗺 В ГЛАВНОЕ МЕНЮ", "🗺 MAIN MENU", "🗺 MENU PRINCIPAL", "🗺 MENÚ PRINCIPAL" } },
        { "MENU_RESUME", new[] { "▶ ПРОДОЛЖИТЬ", "▶ RESUME", "▶ REPRENDRE", "▶ CONTINUAR" } },

        // ========================
        // Controls UI
        // ========================
        { "CONTROL_BUTTONS", new[] { "СТРЕЛКИ (КНОПКИ) ◀ ▶", "ARROWS (BUTTONS) ◀ ▶", "FLÈCHES (BOUTONS) ◀ ▶", "FLECHAS (BOTONES) ◀ ▶" } },
        { "CONTROL_SLIDER", new[] { "ЛИНИЯ (СЛАЙДЕР) ↔", "LINE (SLIDER) ↔", "LIGNE (CURSEUR) ↔", "LÍNEA (DESLIZADOR) ↔" } },
        { "CONTROL_WHEEL", new[] { "КРУГЛЫЙ РУЛЬ ⭕", "ROUND WHEEL ⭕", "VOLANT ROND ⭕", "VOLANTE REDONDO ⭕" } },
        { "STEER_LEFT_MULTILINE", new[] { "◀\nВЛЕВО", "◀\nLEFT", "◀\nGAUCHE", "◀\nIZQUIERDA" } },
        { "STEER_RIGHT_MULTILINE", new[] { "▶\nВПРАВО", "▶\nRIGHT", "▶\nDROITE", "▶\nDERECHA" } },
        { "STEER_LEFT_LABEL", new[] { "◀ ВЛЕВО", "◀ LEFT", "◀ GAUCHE", "◀ IZQUIERDA" } },
        { "STEER_RIGHT_LABEL", new[] { "ВПРАВО ▶", "RIGHT ▶", "DROITE ▶", "DERECHA ▶" } },

        // ========================
        // Victory Modal
        // ========================
        { "WIN_TITLE", new[] { "🏆 ЗАДАНИЕ ВЫПОЛНЕНО", "🏆 TASK COMPLETED", "🏆 MISSION ACCOMPLIE", "🏆 MISIÓN CUMPLIDA" } },
        { "WIN_SUBTITLE", new[] {
            "Грузовик успешно припаркован в целевую зону!",
            "Truck successfully parked in the target zone!",
            "Camion garé avec succès dans la zone cible !",
            "¡Camión estacionado con éxito en la zona objetivo!"
        } },
        { "WIN_CONTINUE", new[] { "▶ ПРОДОЛЖИТЬ", "▶ CONTINUE", "▶ CONTINUER", "▶ CONTINUAR" } },
        { "WIN_MENU", new[] { "🗺 В ОКНО ВЫБОРА УРОВНЯ", "🗺 TO LEVEL SELECT", "🗺 SÉLECTION DU NIVEAU", "🗺 SELECCIÓN DE NIVEL" } },

        // ========================
        // Map Select & Category Menu
        // ========================
        { "MENU_SELECT_MODE", new[] { "ВЫБОР РЕЖИМА", "SELECT MODE", "SÉLECTION DU MODE", "SELECCIÓN DE MODO" } },
        { "MAP_SELECT_TITLE", new[] { "ВЫБОР КАРТЫ", "LEVEL SELECT", "SÉLECTION DU NIVEAU", "SELECCIÓN DE NIVEL" } },
        { "BTN_BACK", new[] { "← НАЗАД", "← BACK", "← RETOUR", "← ATRÁS" } },
        { "BTN_CLOSE", new[] { "✕ ЗАКРЫТЬ", "✕ CLOSE", "✕ FERMER", "✕ CERRAR" } },
        { "MAP_COMPLETED", new[] { "ВЫПОЛНЕНО", "COMPLETED", "TERMINÉ", "COMPLETADO" } },
        { "CAT_OTHER", new[] { "ДРУГИЕ КАРТЫ", "OTHER MAPS", "AUTRES CARTES", "OTROS MAPAS" } },
        { "MAP_NO_MAPS_IN_CAT", new[] {
            "🗺 Пока нет карт в этой категории.",
            "🗺 No maps in this category yet.",
            "🗺 Aucune carte dans cette catégorie.",
            "🗺 No hay mapas en esta categoría."
        } },
        { "MAP_NO_MAPS", new[] {
            "🗺 Пока нет созданных карт.\nСоздайте новую карту через Tools -> Map Builder.",
            "🗺 No maps available yet.\nCreate a new map via Tools -> Map Builder.",
            "🗺 Aucune carte disponible pour le moment.\nCréez une carte via Tools -> Map Builder.",
            "🗺 No hay mapas disponibles aún.\nCrea un mapa nuevo con Tools -> Map Builder."
        } },
        { "PROGRESS_COMPLETED", new[] {
            "Пройдено: {0} из {1} ({2}%)",
            "Completed: {0} of {1} ({2}%)",
            "Terminé : {0} sur {1} ({2}%)",
            "Completado: {0} de {1} ({2}%)"
        } },
        { "PROGRESS_NOT_STARTED", new[] {
            "{0} карт • Не начато",
            "{0} maps • Not started",
            "{0} cartes • Non commencé",
            "{0} mapas • No iniciado"
        } },
        { "PROGRESS_ALL_DONE", new[] {
            "★ Все {0} карт пройдены! (100%)",
            "★ All {0} maps completed! (100%)",
            "★ Toutes les {0} cartes terminées ! (100%)",
            "¡★ Todos los {0} mapas completados! (100%)"
        } },
        { "PROGRESS_ZERO_MAPS", new[] {
            "0 карт",
            "0 maps",
            "0 cartes",
            "0 mapas"
        } },

        // ========================
        // Crash / Jackknife Feedback
        // ========================
        { "JACKKNIFE_TITLE", new[] {
            "💥 СКЛАДЫВАНИЕ! УДАР ТЯГАЧА О ПРИЦЕП! 💥\n<size=22><color=#FFFF66>НАЖМИТЕ [W] (ВПЕРЕД), ЧТОБЫ ВЫПРЯМИТЬ АВТОПОЕЗД</color></size>",
            "💥 JACKKNIFE! TRACTOR HIT THE TRAILER! 💥\n<size=22><color=#FFFF66>PRESS [W] (FORWARD) TO STRAIGHTEN THE RIG</color></size>",
            "💥 MISE EN PORTEFEUILLE ! CHOC TRACTEUR-REMORQUE ! 💥\n<size=22><color=#FFFF66>APPUYEZ SUR [W] (AVANT) POUR REDRESSER L'ATTELAGE</color></size>",
            "💥 ¡TIJERAZO! ¡EL TRACTOR GOLPEÓ EL REMOLQUE! 💥\n<size=22><color=#FFFF66>PRESIONA [W] (ADELANTE) PARA ENDEREZAR EL CAMIÓN</color></size>"
        } },
        { "CRASH_HINT_REVERSE", new[] {
            "НАЖМИТЕ [ТОРМОЗ / S] (НАЗАД), ЧТОБЫ СДАТЬ НАЗАД",
            "PRESS [BRAKE / S] (REVERSE) TO BACK UP",
            "APPUYEZ SUR [FREIN / S] (ARRIÈRE) POUR RECULER",
            "PRESIONA [FRENO / S] (ATRÁS) PARA RETROCEDER"
        } },
        { "CRASH_HINT_FORWARD", new[] {
            "НАЖМИТЕ [ГАЗ / W] (ВПЕРЕД), ЧТОБЫ ОТЪЕХАТЬ",
            "PRESS [GAS / W] (FORWARD) TO DRIVE FORWARD",
            "APPUYEZ SUR [ACCÉLÉRATEUR / W] (AVANT) POUR AVANCER",
            "PRESIONA [ACELERADOR / W] (ADELANTE) PARA AVANZAR"
        } },
        { "CRASH_HIT_PREFIX", new[] { "💥 БУХ! ВРЕЗАЛСЯ В ", "💥 CRASH! HIT ", "💥 CRASH ! HEURTÉ ", "💥 ¡CHOQUE! IMPACTO CON " } },

        // Obstacle names
        { "OBS_TRUCK", new[] { "ПРИПАРКОВАННЫЙ ГРУЗОВИК", "PARKED TRUCK", "CAMION GARÉ", "CAMIÓN ESTACIONADO" } },
        { "OBS_CAR", new[] { "ЛЕГКОВУЮ МАШИНУ", "PASSENGER CAR", "VOITURE", "AUTO" } },
        { "OBS_CONE", new[] { "КОНУС", "TRAFFIC CONE", "CÔNE DE SIGNALISATION", "CONO DE TRÁFICO" } },
        { "OBS_BARREL", new[] { "БОЧКУ", "BARREL", "TONNEAU", "BARRIL" } },
        { "OBS_BARRIER", new[] { "БЕТОННЫЙ БАРЬЕР", "CONCRETE BARRIER", "BARRIÈRE EN BÉTON", "BARRERA DE HORMIGÓN" } },
        { "OBS_BOOTH", new[] { "БУДКУ КПП", "GUARD BOOTH", "POSTE DE GARDE", "CASETA DE CONTROL" } },
        { "OBS_TIRES", new[] { "СТОПКУ ШИН", "TIRE STACK", "PILE DE PNEUS", "PILA DE NEUMÁTICOS" } },
        { "OBS_HYDRANT", new[] { "ПОЖАРНЫЙ ГИДРАНТ", "FIRE HYDRANT", "BORNE D'INCENDIE", "BOCA DE INCENDIO" } },
        { "OBS_BORDER", new[] { "ГРАНИЦУ ПЛОЩАДКИ", "YARD BORDER", "BORDURE DU TERRAIN", "LÍMITE DEL TERRENO" } }
    };

    public static string Get(string key)
    {
        if (table.TryGetValue(key, out string[] translations))
        {
            int idx = (int)CurrentLanguage;
            if (idx >= 0 && idx < translations.Length)
            {
                return translations[idx];
            }
        }
        return key;
    }
}
