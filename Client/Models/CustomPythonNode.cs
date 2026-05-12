using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BayMax.Models
{
    public class CustomPythonNode
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Title")]
        public string Title { get; set; }

        [JsonPropertyName("Category")]
        public string Category { get; set; }

        [JsonPropertyName("Inputs")]
        public List<string> Inputs { get; set; } = new List<string>();

        [JsonPropertyName("Outputs")]
        public List<string> Outputs { get; set; } = new List<string>();

        // Если в ранней версии парсера ключ назывался с маленькой буквы, 
        // мы ловим его в это свойство
        [JsonPropertyName("source_code")]
        public string SourceCodeFallback { get; set; }

        // Если парсер использует заглавные буквы, ловим сюда
        [JsonPropertyName("SourceCode")]
        public string SourceCodeMain { get; set; }

        // Архитектор будет забирать код отсюда (берет то, что не пустое)
        [JsonIgnore]
        public string SourceCode => !string.IsNullOrEmpty(SourceCodeMain) ? SourceCodeMain : SourceCodeFallback;

        [JsonIgnore]
        public bool IsModified { get; set; } = false;

        [JsonIgnore]
        public DateTime LastDiskChange { get; set; }
    }


}