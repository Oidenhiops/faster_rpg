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
    public Utils utils = new Utils();
    public List<ResolutionsInfo> allResolutions = new List<ResolutionsInfo>();
    public Dictionary<TypeLOCS, Dictionary<TypeLanguage, Dictionary<string, DialogData>>> locs = new Dictionary<TypeLOCS, Dictionary<TypeLanguage, Dictionary<string, DialogData>>>();
    public CharactersDBSO charactersDBSO;
    public ItemsDBSO itemsDBSO;
    public SkillsDBSO skillsDBSO;
    public 
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
            InitializeCharacterEquipments();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cargando equipamientos de los personajes: {e}");
        }
        try
        {
            InitializeCharacterFastItems();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cargando items rapidos de los personajes: {e}");
        }
        try
        {
            InitializeCharacterBag();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cargando el inventario de los personajes: {e}");
        }
        try
        {
            InitializeCharacterModels();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cargando los modelos de los personajes: {e}");
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
    void InitializeCharacterEquipments()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<string, CharacterData> characterData in gameDataSlot.characters)
            {
                foreach (KeyValuePair<ItemsDBSO.TypeModel, CharacterData.CharacterItem> item in characterData.Value.equipments)
                {
                    if (item.Value.itemId != 0)
                    {
                        item.Value.itemBaseSO = itemsDBSO.data[item.Value.typeObject][item.Value.itemId];
                    }
                }
            }
        }
    }
    void InitializeCharacterFastItems()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<string, CharacterData> characterData in gameDataSlot.characters)
            {
                foreach (KeyValuePair<int, CharacterData.CharacterItem> fastItem in characterData.Value.fastItems)
                {
                    if (fastItem.Value.itemId != 0)
                    {
                        fastItem.Value.itemBaseSO = itemsDBSO.data[fastItem.Value.typeObject][fastItem.Value.itemId];
                    }
                }
            }
        }
    }
    void InitializeCharacterBag()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<string, CharacterData> characterData in gameDataSlot.characters)
            {
                foreach (KeyValuePair<int, CharacterData.CharacterItem> bagItem in characterData.Value.bag)
                {
                    if (bagItem.Value.itemId != 0)
                    {
                        bagItem.Value.itemBaseSO = itemsDBSO.data[bagItem.Value.typeObject][bagItem.Value.itemId];
                    }
                }
            }
        }
    }
    void InitializeCharacterModels()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<string, CharacterData> characterData in gameDataSlot.characters)
            {
                foreach (KeyValuePair<ItemsDBSO.TypeModel, CharacterData.CharacterSkinInfo> modelItem in characterData.Value.models)
                {
                    if (modelItem.Value.itemId != 0)
                    {
                        modelItem.Value.itemBaseSO = itemsDBSO.data[modelItem.Value.typeObject][modelItem.Value.itemId];
                    }
                }
            }
        }
    }
    void InitializeCharacterSkills()
    {
        foreach (GameDataSlot gameDataSlot in gameDataInfo.gameDataSlots)
        {
            foreach (KeyValuePair<string, CharacterData> characterData in gameDataSlot.characters)
            {
                foreach (KeyValuePair<int, CharacterData.CharacterSkillInfo> skill in characterData.Value.skills)
                {
                    if (skill.Value.skillId != 0)
                    {
                        skill.Value.skillsBaseSO = skillsDBSO.data[skill.Value.skillId];
                    }
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

        List<SerializedDictionary<int, CharacterData.CharacterItem>> bags = new List<SerializedDictionary<int, CharacterData.CharacterItem>>
        {
            new SerializedDictionary<int, CharacterData.CharacterItem>(),
            new SerializedDictionary<int, CharacterData.CharacterItem>(),
            new SerializedDictionary<int, CharacterData.CharacterItem>(),
            new SerializedDictionary<int, CharacterData.CharacterItem>()
        };
        List<SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>> characterStatistics = new List<SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>>
        {
            charactersDBSO.data[1].initialDataSO.CloneStatistics(),
            charactersDBSO.data[1].initialDataSO.CloneStatistics(),
            charactersDBSO.data[1].initialDataSO.CloneStatistics(),
            charactersDBSO.data[1].initialDataSO.CloneStatistics()
        };
        GameDataSlot newSlotData = new GameDataSlot
        {
            isUse = true,
            createdDate = DateTime.Now.ToString(),
            lastSaveDate = DateTime.Now.ToString(),
            currentZone = GameManager.TypeScene.CityScene,
            characters = new SerializedDictionary<string, CharacterData>()
            {
                { randomNames[0], new CharacterData()
                    {
                        level = 1,
                        name = randomNames[0],
                        statistics = characterStatistics[0],
                        equipments = new SerializedDictionary<ItemsDBSO.TypeModel, CharacterData.CharacterItem>()
                        {
                            { ItemsDBSO.TypeModel.Boots, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Boots, 1) },
                            { ItemsDBSO.TypeModel.Front, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Front, 1) },
                            { ItemsDBSO.TypeModel.Gloves, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Gloves, 1) },
                            { ItemsDBSO.TypeModel.Helmet, new CharacterData.CharacterItem() },
                            { ItemsDBSO.TypeModel.Pants, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Pants, 1) },
                            { ItemsDBSO.TypeModel.Pendant, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Pendant, 1) },
                            { ItemsDBSO.TypeModel.Ring, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Ring, 1) },
                            { ItemsDBSO.TypeModel.Weapon, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Weapon, 1) },
                        },
                        fastItems = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 1, 3) },
                            { 1, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 2, 2) },
                            { 2, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 3, 1) },
                            { 3, itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 4, 1) }
                        },
                        bag = bags[0],
                        models = itemsDBSO.GenerateRandomModel()
                    }
                },
                { randomNames[1], new CharacterData()
                    {
                        level = 1,
                        name = randomNames[1],
                        statistics = characterStatistics[1],
                        fastItems = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, new CharacterData.CharacterItem() },
                            { 1, new CharacterData.CharacterItem() },
                            { 2, new CharacterData.CharacterItem() },
                            { 3, new CharacterData.CharacterItem() }
                        },
                        bag = bags[1],
                        models = itemsDBSO.GenerateRandomModel()
                    }
                },
                { randomNames[2], new CharacterData()
                    {
                        level = 1,
                        name = randomNames[2],
                        statistics = characterStatistics[2],
                        fastItems = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, new CharacterData.CharacterItem() },
                            { 1, new CharacterData.CharacterItem() },
                            { 2, new CharacterData.CharacterItem() },
                            { 3, new CharacterData.CharacterItem() }
                        },
                        bag = bags[2],
                        models = itemsDBSO.GenerateRandomModel()
                    }
                },
                { randomNames[3], new CharacterData()
                    {
                        level = 1,
                        name = randomNames[3],
                        statistics = characterStatistics[3],
                        fastItems = new SerializedDictionary<int, CharacterData.CharacterItem>()
                        {
                            { 0, new CharacterData.CharacterItem() },
                            { 1, new CharacterData.CharacterItem() },
                            { 2, new CharacterData.CharacterItem() },
                            { 3, new CharacterData.CharacterItem() }
                        },
                        bag = bags[3],
                        models = itemsDBSO.GenerateRandomModel()
                    }
                },
            }
        };

        foreach (KeyValuePair<string, CharacterData> characterData in newSlotData.characters)
        {
            foreach (KeyValuePair<ItemsDBSO.TypeModel, CharacterData.CharacterItem> equipment in characterData.Value.equipments)
            {
                if (equipment.Value != null && equipment.Value?.itemId != 0)
                {
                    equipment.Value.itemBaseSO = itemsDBSO.data[equipment.Value.typeObject][equipment.Value.itemId];
                    equipment.Value.itemBaseSO.EquipItem(characterData.Value, equipment.Value);
                }
            }
        }

        foreach (KeyValuePair<string, CharacterData> characterData in newSlotData.characters)
        {
            foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterData.Value.statistics)
            {
                if (statistic.Key != CharacterData.TypeStatistic.Exp)
                {
                    statistic.Value.RefreshValue((int)statistic.Key);
                    statistic.Value.SetMaxValue();
                }
            }
        }

        foreach (KeyValuePair<string, CharacterData> characterData in newSlotData.characters)
        {
            for (int i = 0; i < characterData.Value.statistics[CharacterData.TypeStatistic.BagSpace].currentValue; i++)
            {
                characterData.Value.bag.Add(i, new CharacterData.CharacterItem());
            }
        }
        bags[0][0] = itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 1, 3);
        bags[0][1] = itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 2, 2);
        bags[0][2] = itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 3, 1);
        bags[0][3] = itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Weapon, 2, 1);
        bags[0][4] = itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.Helmet, 1);
        bags[0][5] = itemsDBSO.GenerateItem(ItemsDBSO.TypeModel.FastItems, 5);
        return newSlotData;
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
        public SerializedDictionary<string, CharacterData> characters = new SerializedDictionary<string, CharacterData>();
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
    [Serializable]
    public class Utils
    {
        public SerializedDictionary<string, Color> systemColors = new SerializedDictionary<string, Color>();
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