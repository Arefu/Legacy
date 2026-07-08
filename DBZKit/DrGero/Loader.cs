using DrGero.Config;
using System.Text.Json;

namespace DrGero.Loader
{
    public static class GameLibrary
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static IEnumerable<Game> LoadAll(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);

            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Game library directory not found: {directory}");

            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var json = File.ReadAllText(file);
                var game = JsonSerializer.Deserialize<Game>(json, _options);
                if (game != null)
                    yield return game;
            }
        }

        public static Game? FindByCode(string directory, string gameCode)
        {
            return LoadAll(directory).FirstOrDefault(g => g.GAME_CODE.Equals(gameCode, StringComparison.OrdinalIgnoreCase));
        }
    }
}