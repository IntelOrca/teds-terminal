using System;
using System.IO;
using Newtonsoft.Json;

namespace tterm
{
    internal class ConfigurationService
    {
        private const string JsonFileName = "tterm.json";

        private readonly string _jsonPath;

        public Config Config { get; private set; } = new Config();

        public ConfigurationService()
        {
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _jsonPath = Path.Combine(homePath, JsonFileName);
        }

        public Config Load()
        {
            try
            {
                if (File.Exists(_jsonPath))
                {
                    string json = File.ReadAllText(_jsonPath);
                    var config = JsonConvert.DeserializeObject<Config>(json);
                    if (config != null)
                    {
                        Config = config;
                    }
                }
            }
            catch
            {
#if DEBUG
                throw;
#endif
            }
            return Config;
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(_jsonPath, json);
            }
            catch
            {
#if DEBUG
                throw;
#endif
            }
        }
    }
}
