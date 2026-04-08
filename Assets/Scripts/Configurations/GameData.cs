using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using AYellowpaper.SerializedCollections;

public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }
    public GameDataInfo gameDataInfo = new GameDataInfo();
    public SystemDataInfo systemDataInfo = new SystemDataInfo();
    public List<ResolutionsInfo> allResolutions = new List<ResolutionsInfo>();
    public Dictionary<TypeLOCS, Dictionary<TypeLanguage, Dictionary<string, DialogData>>> locs = new Dictionary<TypeLOCS, Dictionary<TypeLanguage, Dictionary<string, DialogData>>>();
    public CharactersDBSO charactersDBSO;
    public CharactersSkinDBSO charactersSkinDBSO;
    public ItemsDBSO itemsDBSO;
    public SkillsDBSO skillsDBSO;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _ = LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public async Awaitable LoadData()
    {
        try
        {
            GetAllResolutions();
            CheckFileExistance();
            LoadGameDataInfo();
            LoadSystemDataInfo();
            LoadLOCS();
            InitializeResolutionData();
            LoadCharacterDataInfo();
            await InitializeAudioMixerData();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public void LoadCharacterDataInfo()
    {
        try
        {
            InitializeBagItems();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cargando items del inventario: {e}");
        }
        try
        {
            InitializeCharacterItems();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cargando items de los personajes: {e}");
        }
        try
        {
            InitializeCharacterSkills();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cargando habilidades de los personajes: {e}");
        }
    }
    public void LoadGameDataInfo()
    {
        ReadGameDataFromJson();
        LoadCharacterDataInfo();
    }
    public void LoadSystemDataInfo()
    {
        systemDataInfo = ReadSystemDataFromJson();
    }
    void InitializeCharacterItems()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<string, CharacterData> characterData in gameDataSlot.selectedCharacters)
            {
                foreach (KeyValuePair<ItemBaseSO.TypeObject, CharacterData.CharacterItem> item in characterData.Value.items)
                {
                    if (item.Value.itemId != 0)
                    {
                        item.Value.itemBaseSO = itemsDBSO.data[item.Value.typeObject][item.Value.itemId];
                    }
                }
            }
        }
    }
    void InitializeCharacterSkills()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<string, CharacterData> characterData in gameDataSlot.selectedCharacters)
            {
                foreach (KeyValuePair<ItemBaseSO.TypeWeapon, SerializedDictionary<string, CharacterData.CharacterSkillInfo>> item in characterData.Value.skills)
                {
                    foreach (KeyValuePair<string, CharacterData.CharacterSkillInfo> skill in item.Value)
                    {
                        if (skill.Value.skillId != "")
                        {
                            skill.Value.skillsBaseSO = skillsDBSO.data[skill.Value.skillId];
                        }
                    }
                }
            }
        }
    }
    void InitializeBagItems()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<int, CharacterData.CharacterItem> item in gameDataSlot.bagItems)
            {
                if (item.Value.itemId != 0)
                {
                    item.Value.itemBaseSO = itemsDBSO.data[ItemBaseSO.TypeObject.None][item.Value.itemId];
                }
            }
        }
    }
    private void GetAllResolutions()
    {
        Resolution[] resolutions = Screen.resolutions;
        Array.Reverse(resolutions);

        HashSet<string> seen = new HashSet<string>();
        foreach (Resolution res in resolutions)
        {
            string key = $"{res.width}x{res.height}";
            if (!seen.Contains(key))
            {
                seen.Add(key);
                allResolutions.Add(new ResolutionsInfo(res.width, res.height));
            }
        }
    }
    void LoadLOCS()
    {
        try
        {
            TextAsset locsSystemEN = Resources.Load<TextAsset>("LOCS/LOC_System_EN");
            locs.Add(TypeLOCS.System, TransformCSV(TypeLanguage.English,locsSystemEN));
        }
        catch
        {
            Debug.LogError("No se encontro el archivo LOC_System_EN");
        }
        try
        {
            TextAsset locsItems = Resources.Load<TextAsset>("LOCS/LOC_Items_EN");
            locs.Add(TypeLOCS.Items, TransformCSV(TypeLanguage.English, locsItems));
        }
        catch
        {
            Debug.LogError("No se encontro el archivo LOC_Items_EN");
        }
        try
        {
            TextAsset locsSkills = Resources.Load<TextAsset>("LOCS/LOC_Skills_EN");
            locs.Add(TypeLOCS.Skills, TransformCSV(TypeLanguage.English, locsSkills));
        }
        catch
        {
            Debug.LogError("No se encontro el archivo LOC_Skills_EN");
        }
        try
        {
            TextAsset locsDialogs = Resources.Load<TextAsset>("LOCS/LOC_Dialogs_EN");
            locs.Add(TypeLOCS.Dialogs, TransformCSV(TypeLanguage.English, locsDialogs));
        }
        catch
        {
            Debug.LogError("No se encontro el archivo LOC_Dialogs_EN");
        }
    }
    Dictionary<TypeLanguage, Dictionary<string, DialogData>> TransformCSV(TypeLanguage language, TextAsset textAsset)
    {
        string[] lines = textAsset.text.Split('\n');
        List<string[]> textData = new List<string[]>();
        foreach (string line in lines)
        {
            List<string> columns = new List<string>(line.Split(';'));
            columns.RemoveAt(columns.Count - 1);
            textData.Add(columns.ToArray());
        }
        Dictionary<TypeLanguage, Dictionary<string, DialogData>> data = new Dictionary<TypeLanguage, Dictionary<string, DialogData>>
        {
            { language, new Dictionary<string, DialogData>() }
        };
        foreach (string[] text in textData)
        {
            data[language].Add(text[0], new DialogData { dialog = text[1], description = text[2] });
        }
        return data;
    }
    public DialogData GetDialog(string id, TypeLOCS typeLOCS)
    {
        if (locs.ContainsKey(typeLOCS) && locs[typeLOCS].ContainsKey(systemDataInfo.configurationsInfo.currentLanguage) && locs[typeLOCS][systemDataInfo.configurationsInfo.currentLanguage].ContainsKey(id))
        {
            return new DialogData
            {
                dialog = locs[typeLOCS][systemDataInfo.configurationsInfo.currentLanguage][id].dialog,
                description = locs[typeLOCS][systemDataInfo.configurationsInfo.currentLanguage][id].description
            };
        }
        return new DialogData
        {
            description = $"NTF {typeLOCS}: {id}",
            dialog = $"NTF {typeLOCS}: {id}"
        };
    }
    public void ChangeLanguage(TypeLanguage language)
    {
        systemDataInfo.configurationsInfo.currentLanguage = language;
        SaveSystemData();
    }
    public void InitializeResolutionData()
    {
        if (GameManager.Instance.currentDevice == GameManager.TypeDevice.PC)
        {
            Screen.SetResolution(
                systemDataInfo.configurationsInfo.resolutionConfiguration.currentResolution.width,
                systemDataInfo.configurationsInfo.resolutionConfiguration.currentResolution.height,
                systemDataInfo.configurationsInfo.resolutionConfiguration.isFullScreen
            );
        }
        else
        {
            Screen.SetResolution(
                Screen.width,
                Screen.height,
                true
            );
        }
    }
    async Awaitable InitializeAudioMixerData()
    {
        try
        {
            await Awaitable.NextFrameAsync();
            float decibelsBGM = 20 * Mathf.Log10(systemDataInfo.configurationsInfo.soundConfiguration.BGMalue / 100);
            float decibelsSFX = 20 * Mathf.Log10(systemDataInfo.configurationsInfo.soundConfiguration.SFXalue / 100);
            if (systemDataInfo.configurationsInfo.soundConfiguration.BGMalue == 0) decibelsBGM = -80;
            if (systemDataInfo.configurationsInfo.soundConfiguration.SFXalue == 0) decibelsSFX = -80;
            AudioManager.Instance.audioMixer.SetFloat(AudioManager.TypeSound.BGM.ToString(), decibelsBGM);
            AudioManager.Instance.audioMixer.SetFloat(AudioManager.TypeSound.SFX.ToString(), decibelsSFX);
            if (systemDataInfo.configurationsInfo.soundConfiguration.isMute)
            {
                AudioManager.Instance.audioMixer.SetFloat(AudioManager.TypeSound.Master.ToString(), -80f);
            }
            else
            {
                GameManager.Instance.StartCoroutine(AudioManager.Instance.FadeIn());
            }
            await Awaitable.NextFrameAsync();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public void SetStartingData()
    {
        GameDataInfo gameData = new GameDataInfo
        {
            gameDataSlots = new List<GameDataSlot>()
            {
                SetStartingSolt(),
                new GameDataSlot(),
                new GameDataSlot()
            }
        };
        SystemDataInfo systemData = new SystemDataInfo();
        systemDataInfo.configurationsInfo.currentLanguage = TypeLanguage.English;
        SetStartingDataSound(ref systemData);
        if (GameManager.Instance.currentDevice == GameManager.TypeDevice.PC) SetStartingResolution(ref systemData);
        gameDataInfo = gameData;
        systemDataInfo = systemData;
        SaveGameData();
    }
    public GameDataSlot SetStartingSolt()
    {
        string[] randomNames = new string[]
        {
            charactersDBSO.GenerateFantasyName(),
            charactersDBSO.GenerateFantasyName(),
            charactersDBSO.GenerateFantasyName(),
            charactersDBSO.GenerateFantasyName()
        };
        SerializedDictionary<int, CharacterData.CharacterItem> bag = new SerializedDictionary<int, CharacterData.CharacterItem>();
        for (int i = 0; i < 10; i++) bag.Add(i, new CharacterData.CharacterItem());
        bag[0] = itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Consumable, 1);
        bag[1] = itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Consumable, 2);
        bag[2] = itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Consumable, 3);
        return new GameDataSlot
        {
            isUse = true,
            createdDate = DateTime.Now.ToString(),
            lastSaveDate = DateTime.Now.ToString(),
            currentZone = GameManager.TypeScene.CityScene,
            selectedCharacters = new SerializedDictionary<string, CharacterData>()
            {
                { randomNames[0], new CharacterData()
                    {
                        name = randomNames[0],
                        level = 1,
                        characterId = 0,
                        characterSkinId = 0,
                        bag = bag,
                        items = new SerializedDictionary<ItemBaseSO.TypeObject, CharacterData.CharacterItem>()
                        {
                            { ItemBaseSO.TypeObject.Boots, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Boots, 1) },
                            { ItemBaseSO.TypeObject.Front, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Front, 1) },
                            { ItemBaseSO.TypeObject.Gloves, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Gloves, 1) },
                            { ItemBaseSO.TypeObject.Helmet, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Helmet, 1) },
                            { ItemBaseSO.TypeObject.Pants, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Pants, 1) },
                            { ItemBaseSO.TypeObject.Pendant, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Pendant, 1) },
                            { ItemBaseSO.TypeObject.Ring, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Ring, 1) },
                            { ItemBaseSO.TypeObject.Utility, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Utility, 1) },
                            { ItemBaseSO.TypeObject.Weapon, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Weapon, 1) },
                        },
                        consumable = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Consumable, 1) },
                            { 1, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Consumable, 2) },
                            { 2, itemsDBSO.GenerateItem(ItemBaseSO.TypeObject.Consumable, 3) },
                        },
                        statistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>()
                        {
                            { CharacterData.TypeStatistic.Hp, new CharacterData.Statistic() { baseValue = 100, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Sp, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Atk, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Hit, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Int, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Def, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Res, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Spd, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Exp, new CharacterData.Statistic() { baseValue = 0, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtv, new CharacterData.Statistic() { baseValue = 5, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtd, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 0 } },
                        }
                    }
                },
                { randomNames[1], new CharacterData()
                    {
                        name = randomNames[1],
                        level = 1,
                        characterId = 0,
                        characterSkinId = 1,
                        statistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>()
                        {
                            { CharacterData.TypeStatistic.Hp, new CharacterData.Statistic() { baseValue = 100, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Sp, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Atk, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Hit, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Int, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Def, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Res, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Spd, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Exp, new CharacterData.Statistic() { baseValue = 0, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtv, new CharacterData.Statistic() { baseValue = 5, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtd, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 0 } },
                        },
                        consumable = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, new CharacterData.CharacterItem() },
                            { 1, new CharacterData.CharacterItem() },
                            { 2, new CharacterData.CharacterItem() },
                        },
                    }
                },
                { randomNames[2], new CharacterData()
                    {
                        name = randomNames[2],
                        level = 1,
                        characterId = 0,
                        characterSkinId = 2,
                        statistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>()
                        {
                            { CharacterData.TypeStatistic.Hp, new CharacterData.Statistic() { baseValue = 100, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Sp, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Atk, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Hit, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Int, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Def, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Res, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Spd, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Exp, new CharacterData.Statistic() { baseValue = 0, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtv, new CharacterData.Statistic() { baseValue = 5, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtd, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 0 } },
                        },
                        consumable = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, new CharacterData.CharacterItem() },
                            { 1, new CharacterData.CharacterItem() },
                            { 2, new CharacterData.CharacterItem() },
                        },
                    }
                },
                { randomNames[3], new CharacterData()
                    {
                        name = randomNames[3],
                        level = 1,
                        characterId = 0,
                        characterSkinId = 3,
                        statistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>()
                        {
                            { CharacterData.TypeStatistic.Hp, new CharacterData.Statistic() { baseValue = 100, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Sp, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Atk, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Hit, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Int, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Def, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Res, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Spd, new CharacterData.Statistic() { baseValue = 10, aptitudeValue = 100 } },
                            { CharacterData.TypeStatistic.Exp, new CharacterData.Statistic() { baseValue = 0, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtv, new CharacterData.Statistic() { baseValue = 5, aptitudeValue = 0 } },
                            { CharacterData.TypeStatistic.Crtd, new CharacterData.Statistic() { baseValue = 50, aptitudeValue = 0 } },
                        },
                        consumable = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, new CharacterData.CharacterItem() },
                            { 1, new CharacterData.CharacterItem() },
                            { 2, new CharacterData.CharacterItem() },
                        },
                    }
                },
            }
        };
    }
    public void SetStartingItems()
    {
        gameDataInfo.gameDataSlots[systemDataInfo.currentGameDataIndex].bagItems = new SerializedDictionary<int, CharacterData.CharacterItem>()
        {
            {0, new CharacterData.CharacterItem()},
            {1, new CharacterData.CharacterItem()},
            {2, new CharacterData.CharacterItem()},
            {3, new CharacterData.CharacterItem()},
            {4, new CharacterData.CharacterItem()},
            {5, new CharacterData.CharacterItem()},
            {6, new CharacterData.CharacterItem()},
            {7, new CharacterData.CharacterItem()},
            {8, new CharacterData.CharacterItem()},
            {9, new CharacterData.CharacterItem()},
            {10, new CharacterData.CharacterItem()},
            {11, new CharacterData.CharacterItem()},
            {12, new CharacterData.CharacterItem()},
            {13, new CharacterData.CharacterItem()},
            {14, new CharacterData.CharacterItem()},
            {15, new CharacterData.CharacterItem()},
        };
    }
    public void SetInitalPositions()
    {
        gameDataInfo.gameDataSlots[systemDataInfo.currentGameDataIndex].positionsSave = new SerializedDictionary<string, Vector3Int>()
        {
            { GameManager.TypeScene.CityScene.ToString(), new Vector3Int(0,0,0) },
            { GameManager.TypeScene.TavernScene.ToString(), new Vector3Int(0,0,0) },
        };
    }
    void SetStartingDataSound(ref SystemDataInfo dataInfo)
    {
        dataInfo.configurationsInfo.soundConfiguration.MASTERValue = 25;
        dataInfo.configurationsInfo.soundConfiguration.BGMalue = 25;
        dataInfo.configurationsInfo.soundConfiguration.SFXalue = 25;
    }
    void SetStartingResolution(ref SystemDataInfo dataInfo)
    {
        Screen.SetResolution(allResolutions[0].width, allResolutions[0].height, true);
        dataInfo.configurationsInfo.resolutionConfiguration.isFullScreen = true;
        dataInfo.configurationsInfo.resolutionConfiguration.currentResolution = new ResolutionsInfo(allResolutions[0].width, allResolutions[0].height);
    }
    [NaughtyAttributes.Button]
    public void SaveGameData()
    {
        WriteGameDataToJson();
    }
    [NaughtyAttributes.Button]
    public void SaveSystemData()
    {
        WriteSystemDataToJson();
    }
    void CheckFileExistance()
    {
        if (!File.Exists(DataPath(TypeSaveData.SystemDataInfo)))
        {
            File.Create(DataPath(TypeSaveData.SystemDataInfo)).Close();
            SetStartingData();
            string gameDataString = JsonUtility.ToJson(gameDataInfo);
            string systemDataString = JsonUtility.ToJson(systemDataInfo);
            File.WriteAllText(DataPath(TypeSaveData.SystemDataInfo), gameDataString);
            File.WriteAllText(DataPath(TypeSaveData.SystemDataInfo), systemDataString);
        }
    }
    GameDataInfo ReadGameDataFromJson()
    {
        string dataString;
        string jsonFilePath = DataPath(TypeSaveData.GameDataInfo);
        dataString = File.ReadAllText(jsonFilePath);
        gameDataInfo = JsonUtility.FromJson<GameDataInfo>(dataString);
        return gameDataInfo;
    }
    SystemDataInfo ReadSystemDataFromJson()
    {
        string dataString;
        string jsonFilePath = DataPath(TypeSaveData.SystemDataInfo);
        dataString = File.ReadAllText(jsonFilePath);
        systemDataInfo = JsonUtility.FromJson<SystemDataInfo>(dataString);
        return systemDataInfo;
    }
    public void WriteGameDataToJson()
    {
        string jsonFilePath = DataPath(TypeSaveData.GameDataInfo);
        string dataString = JsonUtility.ToJson(gameDataInfo);
        File.WriteAllText(jsonFilePath, dataString);
    }
    public void WriteSystemDataToJson()
    {
        string jsonFilePath = DataPath(TypeSaveData.SystemDataInfo);
        string dataString = JsonUtility.ToJson(systemDataInfo);
        File.WriteAllText(jsonFilePath, dataString);
    }
    string DataPath(TypeSaveData typeSaveData)
    {
        return Path.Combine(Application.persistentDataPath, typeSaveData + ".json");
    }
    [Serializable]
    public class GameDataInfo
    {
        public List<GameDataSlot> gameDataSlots = new List<GameDataSlot>();
    }
    [Serializable]
    public class GameDataSlot
    {
        public bool isUse = false;
        public string createdDate = "";
        public string lastSaveDate = "";
        public GameManager.TypeScene currentZone;
        public SerializedDictionary<string, Vector3Int> positionsSave = new SerializedDictionary<string, Vector3Int>();
        public SerializedDictionary<int, CharacterData.CharacterItem> bagItems = new SerializedDictionary<int, CharacterData.CharacterItem>();
        public SerializedDictionary<string, CharacterData> selectedCharacters = new SerializedDictionary<string, CharacterData>();
        public SerializedDictionary<string, CharacterData> bagCharacters = new SerializedDictionary<string, CharacterData>();
        public SerializedDictionary<string, CharacterData> dieCharacters = new SerializedDictionary<string, CharacterData>();
        // public SerializedDictionary<string, InitialBGMSoundsConfigSO.BGMScenesData> bgmSceneData = new SerializedDictionary<string, InitialBGMSoundsConfigSO.BGMScenesData>();
    }
    [Serializable]
    public class ConfigurationsInfo
    {
        public TypeLanguage _currentLanguage;
        public Action<TypeLanguage> OnLanguageChange;
        public TypeLanguage currentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnLanguageChange?.Invoke(_currentLanguage);
                }
            }
        }
        public ResolutionConfiguration resolutionConfiguration = new ResolutionConfiguration();
        public SoundConfiguration soundConfiguration = new SoundConfiguration();
    }
    [Serializable]
    public class SoundConfiguration
    {
        public bool isMute = false;
        public float MASTERValue;
        public float BGMalue;
        public float SFXalue;
    }
    [Serializable]
    public class ResolutionConfiguration
    {
        public bool isFullScreen = false;
        public ResolutionsInfo currentResolution;
    }
    [Serializable]
    public class ResolutionsInfo
    {
        public int width = 0;
        public int height = 0;
        public ResolutionsInfo(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }
    [Serializable]
    public class SystemDataInfo
    {
        public int currentGameDataIndex = 0;
        public ConfigurationsInfo configurationsInfo = new ConfigurationsInfo();
    }
    public class DialogData
    {
        public string dialog;
        public string description;
    }
    public enum TypeLanguage
    {
        English = 0,
        Español = 1,
    }
    public enum TypeLOCS
    {
        None = 0,
        System = 1,
        Dialogs = 2,
        Items = 3,
        Skills = 4,
        Chars = 5
    }
    public enum TypeSaveData
    {
        None = 0,
        GameDataInfo = 1,
        SystemDataInfo = 2
    }
}