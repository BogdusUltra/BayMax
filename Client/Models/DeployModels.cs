using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace BayMax.Models
{
    public class DeployRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "deploy_request";

        [JsonPropertyName("client_public_key")]
        public string ClientPublicKey { get; set; }

        [JsonPropertyName("nodes")]
        public List<DeployNode> Nodes { get; set; } = new List<DeployNode>();
    }

    public class DeployNode
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("publishers")]
        public Dictionary<string, int> Publishers { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("subscribers")]
        public Dictionary<string, string> Subscribers { get; set; } = new Dictionary<string, string>();
    }
}
