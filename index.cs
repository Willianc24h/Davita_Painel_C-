using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Programa
{
    static async Task Main(string[] args)
    {
        string clientIdDavita = "591c97684fa54e3790897fdd0ecafac1";
        string clientSecretDavita = "ovcnLZ_V4uq4ijYzewn2n3R2RqhYj-PA5azGFdG20W3p4vT7BB0YyaKArxwIM_KVYqOVwMqLasfEjy2pMchDVg";

        var talkDic = new Dictionary<string, string>
        {
            { "nome_fila", "live_contacts_in_queue" },
            { "id_fila", "2fbf3f809fca4352a5b458da347a6f9f" },
            { "nome_tempo_atendimento", "avg_handle_time" },
            { "id_tempo_atendimento", "35267ca0b4c6410895ddd45ad4043a0e" },
            { "nome_nivel_servico", "service_level" },
            { "id_nivel_servico", "ab8e693f64d741e081d117197929c2d9" },
            { "nome_live_users", "live_users_logged_in_by_status" },
            { "id_live_users", "2d3f540ccecb4776902bd317fe2e411d" },
            { "nome_Tempo__Médio_Espera", "avg_wait_time_by_ring_group" },
            { "id_Tempo_Médio_Espera", "7c9178ff86eb4b51ad86f75f3fc5cfe4" },
            { "nome_Maior_Tempo_Espera", "longest_wait_time_by_ring_group" },
            { "id_Maior_Tempo_Espera", "645cb1fe79a244a9a151592453d66c78" },
            { "nome_ligacoes_atendidas", "answered_contacts" },
            { "id_ligacoes_atendidas", "4e9788986d6845cd8f0d25d31f849bdf" },
            { "nome_ligacoes_perdidas", "missed_contacts" },
            { "id_ligacoes_perdidas", "91d2e348dbd64c4a8dde729356575c5d" },
            { "nome_ligacoes_total", "inbound_contacts" },
            { "id_ligacoes_total", "b7c50e0ae810452181a1d7c7f14cf8ba" }
        };

        while (true)
        {
            try
            {
                string authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientIdDavita}:{clientSecretDavita}"));

                using var httpClient = new HttpClient();

                var urlApi = "https://davitabr.talkdeskid.com/oauth/token";
                var payload = new Dictionary<string, string> { { "grant_type", "client_credentials" } };
                var request = new HttpRequestMessage(HttpMethod.Post, urlApi)
                {
                    Headers = { { "Authorization", $"Basic {authorization}" } },
                    Content = new FormUrlEncodedContent(payload)
                };

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());
                string accessToken = responseData["access_token"];

                var url = "https://api.talkdeskapp.com/live-subscriptions";
                var headers = new Dictionary<string, string>
                {
                    { "accept", "application/json" },
                    { "Authorization", $"Bearer {accessToken}" },
                    { "content-type", "application/json" }
                };

                var dataFormatada = DateTime.Now.ToString("yyyy-MM-dd") + "T00:00:00";

                // StatusUsuários
                var bodyStatusUsers = new
                {
                    queries = new[]
                    {
                        new
                        {
                            id = talkDic["id_live_users"],
                            metadata = new { name = talkDic["nome_live_users"] },
                            par = new { },
                            filters = new { range = new { from = dataFormatada } }
                        }
                    }
                };

                var responseStatusUsers = await httpClient.PostAsync(url, CreateHttpContent(bodyStatusUsers, headers));
                responseStatusUsers.EnsureSuccessStatusCode();
                var dataStatusUsers = JsonConvert.DeserializeObject<Dictionary<string, object>>(await responseStatusUsers.Content.ReadAsStringAsync());
                var streamHrefUrlStatusUsers = (string)((Dictionary<string, dynamic>)dataStatusUsers["_links"])["stream"]["href"];
                
                var emPausa = 0;
                var logados = 0;
                var disponivel = 0;
                var emAtendimento = 0;

                using (var responseStream = await httpClient.GetStreamAsync(streamHrefUrlStatusUsers))
                using (var streamReader = new System.IO.StreamReader(responseStream))
                {
                    var chunk = await streamReader.ReadToEndAsync();
                    var resultFila = chunk.Split("data:")[1];
                    var dadosJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultFila);
                    foreach (var item in (IEnumerable<Dictionary<string, object>>)dadosJson["result"])
                    {
                        var nome = item["_key"].ToString();
                        var valor = Convert.ToInt32(item["_value"]);

                        if (nome == "away" || nome == "away_ambulatrio" || nome == "away_ativo" || nome == "away_backoffice" || nome == "away_banheiro" || nome == "away_descanso" || nome == "away_lanche" || nome == "away_reunio")
                        {
                            emPausa += valor;
                        }
                        else if (nome == "_total")
                        {
                            logados += valor;
                        }
                        else if (nome == "available")
                        {
                            disponivel += valor;
                        }
                        else if (nome == "after_call_work" || nome == "busy")
                        {
                            emAtendimento += valor;
                        }
                    }
                }

                // Fila
                var bodyFila = new
                {
                    queries = new[]
                    {
                        new
                        {
                            id = talkDic["id_fila"],
                            metadata = new { name = talkDic["nome_fila"] },
                            para = new { },
                            filters = new { range = new { from = dataFormatada } }
                        }
                    }
                };

                var responseFila = await httpClient.PostAsync(url, CreateHttpContent(bodyFila, headers));
                responseFila.EnsureSuccessStatusCode();
                var dataFila = JsonConvert.DeserializeObject<Dictionary<string, object>>(await responseFila.Content.ReadAsStringAsync());
                var streamHrefUrlFila = (string)((Dictionary<string, dynamic>)dataFila["_links"])["stream"]["href"];

                using (var responseStream = await httpClient.GetStreamAsync(streamHrefUrlFila))
                using (var streamReader = new System.IO.StreamReader(responseStream))
                {
                    var chunk = await streamReader.ReadToEndAsync();
                    var indiceResult = chunk.IndexOf("\"result\":");
                    var substring = chunk.Substring(indiceResult);
                    var indiceColcheteAberto = substring.IndexOf('[');
                    var indiceColcheteFechado = substring.IndexOf(']');
                    var resultadoSubstring = substring.Substring(indiceColcheteAberto, indiceColcheteFechado - indiceColcheteAberto + 1);
                    var resultadoJson = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(resultadoSubstring);
                    var fila = resultadoJson[0]["_value"].ToString();
                }

                Console.WriteLine($"Em pausa: {emPausa}");
                Console.WriteLine($"Logados: {logados}");
                Console.WriteLine($"Disponível: {disponivel}");
                Console.WriteLine($"Em atendimento: {emAtendimento}");
                Console.WriteLine($"Fila: {Fila}");

                // Aqui você poderia chamar a função Request() com os dados que coletou
                // Request(data_hoje,Fila,em_pausa,logados,disponivel,em_atendimento);

                await Task.Delay(15000); // Espera 15 segundos
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
                await Task.Delay(30000); // Espera 30 segundos antes de tentar novamente
            }
        }
    }

    private static HttpContent CreateHttpContent(object content, Dictionary<string, string> headers)
    {
        var jsonContent = JsonConvert.SerializeObject(content);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        return httpContent;
    }
}
