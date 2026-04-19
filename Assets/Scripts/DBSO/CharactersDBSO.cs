using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "CharactersDB", menuName = "ScriptableObjects/DB/CharactersDB", order = 1)]
public class CharactersDBSO : ScriptableObject
{
    public SerializedDictionary<int, SerializedDictionary<int, CharacterInfo>> data = new SerializedDictionary<int, SerializedDictionary<int, CharacterInfo>>();
    [System.Serializable] public class CharacterInfo
    {
        public bool isUnlocked;
        public InitialDataSO initialDataSO;
    }
    public InitialDataSO GetRandomInitialDataSO()
    {
        int randomKey = Random.Range(1, data.Count);
        return data[randomKey][Random.Range(0, data[randomKey].Count)].initialDataSO;
    }
    public string GenerateFantasyName()
    {
        string[] syllablesStart = { "Ka", "Lo", "Mi", "Ra", "Th", "El", "Ar", "Va", "Zy", "Xe", "Lu", "Na" };
        string[] syllablesMiddle = { "ra", "en", "or", "il", "um", "ar", "is", "al", "on", "ir" };
        string[] syllablesEnd = { "th", "dor", "ion", "mir", "rak", "len", "var", "oth", "us", "iel" };

        int pattern = Random.Range(0, 3);
        string name = "";

        switch (pattern)
        {
            case 0:
                name = string.Concat(
                    syllablesStart[Random.Range(0, syllablesStart.Length)],
                    syllablesEnd[Random.Range(0, syllablesEnd.Length)]
                );
                break;
            case 1:
                name = string.Concat(
                    syllablesStart[Random.Range(0, syllablesStart.Length)],
                    syllablesMiddle[Random.Range(0, syllablesMiddle.Length)],
                    syllablesEnd[Random.Range(0, syllablesEnd.Length)]
                );
                break;
            case 2:
                name = string.Concat(
                    syllablesStart[Random.Range(0, syllablesStart.Length)],
                    syllablesMiddle[Random.Range(0, syllablesMiddle.Length)],
                    syllablesMiddle[Random.Range(0, syllablesMiddle.Length)],
                    syllablesEnd[Random.Range(0, syllablesEnd.Length)]
                );
                break;
        }

        return name;
    }
}
