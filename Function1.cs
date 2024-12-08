/*
    Development Information: I used many information sources to complete this assignment. I found a youTube video where i learned how to modify the 
    Azure function to take in a variable. Also I was able to learn how to apply a JSON format to the response from the same video. I used stackoverflow and the
    Microsoft website to figure out how to use the GetAsync function and to read the response. The logic for how to reattempt API calls came from chatGPT which seemed
    more simple than trying to recursively call the function. The rest of the function itself was generated from Visual Studio when initially creating the Azure Function. Lastly,
    i gave the entire function to chatGPT to clean, lint and make any efficiency changes.
*/

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace IIR_Homework_12_6_2024
{
    public class GetEventData
    {
        private readonly ILogger<GetEventData> _logger;

        //creating a string to hold the API Url.
        private readonly string connectionString = "https://iir-interview-homework-ddbrefhkdkcgdpbs.eastus2-01.azurewebsites.net/api/v1.0/event-data";

        public GetEventData(ILogger<GetEventData> logger)
        {
            _logger = logger;
        }

        [Function("GetEventData")]
        //set function triggers, route and placed an input variable in the URL route as required by the coding assignment.
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getEventData/{eventId}")] HttpRequestData req,int eventId)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            //implementing attempt counter and max number of retries allowed.
            int attempt = 0;
            const int maxRetry = 5;

            //beginning API attempts
            while (attempt < maxRetry)
            {
                // I remember being asked about usings so after researching online i believe this is the correct context for the httpClient
                using (var httpClient = new HttpClient())
                {
                    //implementing try and catch for API attempts
                    try
                    {
                        //we await the response from the API
                        HttpResponseMessage apiResponse = await httpClient.GetAsync(connectionString);

                        //first we check if the response from the API produces a success code.
                        if (apiResponse.IsSuccessStatusCode)
                        {
                            //if the response is a success we read the repsonse string from the API
                            var apiResponseText = await apiResponse.Content.ReadAsStringAsync();

                            //if the API returns a null or empty string then we log the blank data received and we reattempt another API call
                            if (string.IsNullOrEmpty(apiResponseText))
                            {
                                _logger.LogError($"Received blank data from API. Commencing API Retry. Attempt#: {attempt + 1}");
                            }
                            //if the API returns some value in the string we deserialize the string using the model at the bottom of this file. 
                            else
                            {
                                // Deserialize the API response into a List of Events
                                var jsonResponse = JsonSerializer.Deserialize<List<ListOfEvents>>(apiResponseText);

                                // We search for the specific event that is being looked for
                                var foundEvent = jsonResponse?.FirstOrDefault(s => s.id == eventId);
                                var responseText = req.CreateResponse(HttpStatusCode.OK);

                                // If the event is found we then apply a JSON format that is asked for in the coding assignment.
                                if (foundEvent != null)
                                {

                                    await responseText.WriteAsJsonAsync(new
                                    {
                                        Name = foundEvent.name,
                                        Days = (foundEvent.dateEnd.GetValueOrDefault() - foundEvent.dateStart.GetValueOrDefault()).Days,
                                        WebsiteURL = foundEvent.url
                                    });
                                    return responseText;
                                }
                                //if the Event being asked for in not found we return a simple not found string.
                                else
                                {
                                    await responseText.WriteStringAsync("Event Not Found.");
                                    return responseText;
                                }
                            }
                        }
                        //if the API does not return a success then we log the attempt and retry the API call. 
                        else
                        {
                            _logger.LogError($"Received non-success from API. Commencing API Retry. Attempt#: {attempt + 1}");
                        }
                    }
                    //if for some reason the API call has any other error we log that error and make our next attempt. 
                    catch (Exception ex)
                    {
                        _logger.LogError($"An error has occurred calling the API: {connectionString}. Error Message: {ex.Message}");
                        _logger.LogError($"Retrying API call. Attempt#: {attempt + 1}");
                    }
                }
                //increment attempts 
                attempt++;
            }

            //if all attempts have been attempted then we log that the limit has been reached and display this message to the user. 
            _logger.LogError($"Max number of API attempts have been reached. {attempt} total attempts were made. Data retrieval has failed.");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Max number of API attempts have been reached. {attempt} total attempts were made. Data retrieval has failed.");
            return errorResponse;
        }

        // Model for List of event from the swagger page providing in the coding assignment. I did not want to make anything optional so i applied require to everything.
        public class ListOfEvents
        {
            public required int id { get; set; }
            public required string name { get; set; }
            public required string program { get; set; }
            public required DateTime? dateStart { get; set; }
            public required DateTime? dateEnd { get; set; }
            public required string url { get; set; }
            public required string owner { get; set; }
        }
    }
}
