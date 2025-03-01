using System;
using System.Diagnostics;

namespace Solvex.Internal.UGPM
{
    internal class CredentialHelper
    {
        public static (string username, string password) GetCredentials(string protocol, string host)
        {
            // Формируем ввод для команды GCM
            string inputData = $"protocol={protocol}\nhost={host}\n\n";

            // Создаем процесс для вызова `git credential fill`
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "credential fill",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // Запускаем процесс
            process.Start();

            // Передаем данные через стандартный ввод
            using (var streamWriter = process.StandardInput)
            {
                streamWriter.Write(inputData);
            }

            // Читаем стандартный вывод
            string output = process.StandardOutput.ReadToEnd();

            // Ожидаем завершения процесса
            process.WaitForExit();

            // Проверяем на ошибки
            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"Error fetching credentials: {error}");
            }

            // Разбираем результат
            string username = null;
            string password = null;

            foreach (string line in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("username="))
                    username = line.Substring("username=".Length);
                else if (line.StartsWith("password="))
                    password = line.Substring("password=".Length);
            }

            if (username == null || password == null)
                throw new Exception("Failed to retrieve credentials from GCM.");

            return (username, password);
        }
    }
}
