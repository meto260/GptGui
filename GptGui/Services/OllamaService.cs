using GptGui.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GptGui.Services {
    public class OllamaService {
        private readonly string _baseUrl = "http://localhost:11434";
        private readonly RestClient _client;

        public OllamaService() {
            _client = new RestClient(_baseUrl);
        }

        public async Task<List<string>> GetAvailableModels() {
            var request = new RestRequest("/api/tags", Method.Get);
            var response = await _client.ExecuteAsync(request);
            var result = JsonConvert.DeserializeObject<dynamic>(response.Content);
            return ((JArray)result.models).Select(m => (string)m["name"]).ToList();
        }

        public async Task<string> GetCompletion(string model, string prompt, List<ChatMessage> history = null) {
            var request = new RestRequest("/api/chat", Method.Post);
            var messages = new List<object>();

            if (history != null) {
                messages.AddRange(history.Select(m => new { role = m.Role, content = m.Content }));
            }

            messages.Add(new { role = "user", content = prompt });

            request.AddJsonBody(new {
                model = model,
                messages = messages,
                stream = false  // Stream'i kapatıyoruz
            });

            var response = await _client.ExecuteAsync(request);

            // Response içeriğini düzgün şekilde parse ediyoruz
            try {
                var result = JsonConvert.DeserializeObject<dynamic>(response.Content);
                return result.message.content.ToString();
            }
            catch {
                // Eğer normal parse işlemi başarısız olursa, satır satır okumayı deneyelim
                using (var reader = new StringReader(response.Content)) {
                    string line;
                    string lastResponse = "";

                    while ((line = reader.ReadLine()) != null) {
                        if (!string.IsNullOrEmpty(line)) {
                            try {
                                var lineResult = JsonConvert.DeserializeObject<dynamic>(line);
                                if (lineResult.message != null && lineResult.message.content != null) {
                                    lastResponse = lineResult.message.content.ToString();
                                }
                            }
                            catch { }
                        }
                    }

                    return lastResponse;
                }
            }
        }

        public async Task<string> GetCompletionWithFile(string model, string prompt, byte[] fileContent, string fileName, List<ChatMessage> history = null) {
            var request = new RestRequest("/api/chat", Method.Post);
            var messages = new List<object>();

            if (history != null) {
                messages.AddRange(history.Select(m => new { role = m.Role, content = m.Content }));
            }

            // Base64 encode file content
            var base64File = Convert.ToBase64String(fileContent);
            var fileMessage = $"[File: {fileName}]\n{base64File}";

            messages.Add(new { role = "user", content = $"{prompt}\n{fileMessage}" });

            request.AddJsonBody(new {
                model = model,
                messages = messages,
                stream = false
            });

            var response = await _client.ExecuteAsync(request);

            try {
                var result = JsonConvert.DeserializeObject<dynamic>(response.Content);
                return result.message.content.ToString();
            }
            catch {
                // Hata durumu yönetimi aynı kalacak
                return "Error processing file";
            }
        }
    }
}
