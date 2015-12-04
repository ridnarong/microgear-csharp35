using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using RestSharp;
using System.Collections.Specialized;
using RestSharp.Authenticators;
using System.Web;

namespace Microgear
{
    public static class Microgear
    {
        public static string gearauthsite = "http://gearauth.netpie.io:8080";
        public static string gearauthrequesttokenendpoint = gearauthsite + "/oauth/request_token";
        public static string gearauthaccesstokenendpoint = gearauthsite + "/oauth/access_token";
        public static string gearkey = "";
        public static string gearsecret = "";
        public static string appid = "";
        public static string gearname = null;
        public static string accesstoken = null;
        public static string requesttoken = null;
        public static string client = null;
        public static string scope = "";
        public static string gearexaddress = null;
        public static string gearexport = null;
        public static string mqtt_client = null;
        public static string accesssecret = null;

    }

    public class requesttoken
    {
        public string token { get; set; }
        public string secret { get; set; }
        public string verifier { get; set; }
    }

    public class accesstoken
    {
        public string token { get; set; }
        public string secret { get; set; }
        public string endpoint { get; set; }
    }

    public class tokencache
    {
        public requesttoken requesttoken { get; set; }
        public accesstoken accesstoken { get; set; }
    }

    public class token
    {
        public tokencache _ { get; set; }
    }

    public class Client
    {
        private System.Timers.Timer aTimer;
        private tokencache tokencache;
        private token token;
        private accesstoken accesstoken;
        private requesttoken requesttoken;
        private cache cache;
        private MqttClient mqtt_client;
        private List<string> subscribe_list = new List<string>();
        private List<List<string>> pubilsh_list = new List<List<string>>();
        public Action on_disconnect;
        public Action on_present;
        public Action on_absent;
        public Action on_connect;
        public Action<string, string> on_message;

        private void do_nothing()
        {


        }
        private void do_nothing(string i, string j)
        {


        }
        public void create(string gearkey, string gearsecret, string appid)
        {
            this.on_disconnect = do_nothing;
            this.on_present = do_nothing;
            this.on_absent = do_nothing;
            this.on_connect = do_nothing;
            this.on_message = do_nothing;
            this.cache = new cache();
            this.tokencache = new tokencache();
            this.accesstoken = new accesstoken();
            this.requesttoken = new requesttoken();
            this.token = new token();
            Microgear.gearkey = gearkey;
            Microgear.gearsecret = gearsecret;
            Microgear.appid = appid;
        }

        public void connect()
        {
            this.aTimer = new System.Timers.Timer();
            while (Microgear.accesstoken == null)
            {
                get_token();
                aTimer.Interval = 10000;
            }
            this.mqtt_client = new MqttClient(Microgear.gearexaddress);
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            string username = Microgear.gearkey + "%" + unixTimestamp;
            string pass = CreateToken(Microgear.accesssecret + "&" + Microgear.gearsecret, Microgear.accesstoken + "%" + username);
            var client = this.mqtt_client.Connect(Microgear.accesstoken, username, pass);

            this.mqtt_client.MqttMsgPublishReceived += HandleClientMqttMsgPublishReceived;
            this.mqtt_client.MqttMsgPublished += MqttMsgPublished;
            this.mqtt_client.MqttMsgDisconnected += ConnectionClosedEventHandler;
            this.mqtt_client.MqttMsgSubscribed += MqttMsgSubscribed;
            this.on_connect();
            foreach (string topic in this.subscribe_list)
            {
                this.mqtt_client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            }
            foreach (List<string> pubilsh in this.pubilsh_list)
            {
                Console.WriteLine(pubilsh[0] + " " + pubilsh[1]);
                string strValue = Convert.ToString(pubilsh[1]);
                this.mqtt_client.Publish(pubilsh[0], Encoding.UTF8.GetBytes(pubilsh[1]), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            }
            this.subscribe_list = new List<string>();
            this.pubilsh_list = new List<List<string>>();
        }
        private void ConnectionClosedEventHandler(object sender, System.EventArgs e)
        {
            this.on_disconnect();
        }
        private void MqttMsgPublished(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishedEventArgs e)
        {
        }
        private void HandleClientMqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            foreach (string topic in this.subscribe_list)
            {
                this.mqtt_client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            }
            this.subscribe_list = new List<string>();
            foreach (List<string> pubilsh in this.pubilsh_list)
            {
                string strValue = Convert.ToString(pubilsh[1]);
                this.mqtt_client.Publish(pubilsh[0], Encoding.UTF8.GetBytes(pubilsh[1]), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            }
            this.pubilsh_list = new List<List<string>>();
            this.on_message(e.Topic, Encoding.UTF8.GetString(e.Message));
        }
        private void MqttMsgSubscribed(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgSubscribedEventArgs e)
        {
        }
        public void subscribe(string topic)
        {
            topic = "/" + Microgear.appid + topic;
            this.subscribe_list.Add(topic);
        }
        public void publish(string topic, string message)
        {
            List<string> publich = new List<string>();
            publich.Add("/" + Microgear.appid + topic);
            publich.Add(message);
            this.pubilsh_list.Add(publich);
        }


        public void setname(string topic)
        {
            Microgear.gearname = topic;
            subscribe("/gearname/" + topic);
        }

        public void chat(string topic, string message)
        {
            this.publish("/gearname/" + topic, message);
        }

        private void get_token()
        {
            var cached = this.cache.get_item("microgear.cache");
            if (cached == null)
            {
                this.cache.set_item(null, "microgear.cache");
                cached = this.cache.get_item("microgear.cache");
            }
            if (cached.accesstoken != null)
            {
                string[] endpoint = cached.accesstoken.endpoint.Split(':');
                Microgear.accesstoken = cached.accesstoken.token;
                Microgear.accesssecret = cached.accesstoken.secret;
                Microgear.gearexaddress = endpoint[1].Split('/')[2];
                Microgear.gearexport = endpoint[2];
            }
            else
            {
                this.tokencache = cached;
                forToken();
            }
        }
        public void forToken()
        {
            var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var verifier = new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
            Uri baseUrl = new Uri(Microgear.gearauthsite);
            RestClient client = new RestClient(baseUrl.AbsoluteUri)
            {
                Authenticator = OAuth1Authenticator.ForRequestToken(Microgear.gearkey, Microgear.gearsecret, "scope=" + Microgear.scope + "&appid=" + Microgear.appid + "&verifier=" + verifier)
            };
            RestRequest request = new RestRequest("oauth/request_token", Method.POST);
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            IRestResponse response = client.Execute(request);
            NameValueCollection qs = HttpUtility.ParseQueryString(response.Content);
            string oauthToken = qs["oauth_token"];
            string oauthTokenSecret = qs["oauth_token_secret"];
            this.requesttoken.token = oauthToken;
            this.requesttoken.secret = oauthTokenSecret;
            this.requesttoken.verifier = verifier;
            this.tokencache.requesttoken = this.requesttoken;
            string url = client.BuildUri(request).ToString();
            request = new RestRequest("oauth/access_token", Method.POST);
            request.RequestFormat = DataFormat.Json;
            client.Authenticator = OAuth1Authenticator.ForAccessToken(Microgear.gearkey, Microgear.gearsecret, oauthToken,
                oauthTokenSecret, verifier);
            response = client.Execute(request);
            qs = HttpUtility.ParseQueryString(response.Content);
            oauthToken = qs["oauth_token"];
            oauthTokenSecret = qs["oauth_token_secret"];
            string endpoint = qs["endpoint"];
            url = client.BuildUri(request).ToString();
            this.accesstoken.token = oauthToken;
            this.accesstoken.secret = oauthTokenSecret;
            this.accesstoken.endpoint = endpoint;
            this.tokencache.accesstoken = this.accesstoken;
            this.token._ = this.tokencache;
            this.cache.set_item(this.token, "microgear.cache");
        }
        private string CreateToken(string secret, string message)
        {
            secret = secret ?? "";
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = encoding.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha1 = new HMACSHA1(keyByte))
            {
                byte[] hashmessage = hmacsha1.ComputeHash(messageBytes);
                return Convert.ToBase64String(hashmessage).Trim();
            }
        }
    }

    public class cache
    {
        public tokencache get_item(string key)
        {
            string path = Directory.GetCurrentDirectory();
            string pathkey = System.IO.Path.Combine(path, key);

            if (!System.IO.File.Exists(pathkey))
            {
                return null;
            }
            else
            {
                string text = System.IO.File.ReadAllText(pathkey);
                if (text.Length > 0)
                {
                    return GetDict(text)._;
                }
                tokencache tokencache = new tokencache();
                tokencache.accesstoken = null;
                tokencache.requesttoken = null;
                return tokencache;
            }
        }
        public void set_item(token token, string key)
        {
            string path = Directory.GetCurrentDirectory();
            string pathkey = System.IO.Path.Combine(path, key);
            if (!System.IO.File.Exists(pathkey))
            {
                System.IO.FileStream cache_file = System.IO.File.Create(pathkey);
                cache_file.Dispose();
            }
            if (token != null)
            {
                string ans = JsonConvert.SerializeObject(token, Formatting.Indented);
                System.IO.File.WriteAllText(@pathkey, ans);
            }
        }
        private token GetDict(string text)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            text = text.Replace(" ", "");
            string[] words = text.Split('\n');
            token token = new token();
            tokencache tokencache = new tokencache();
            accesstoken accesstoken = new accesstoken();
            requesttoken requesttoken = new requesttoken();
            requesttoken.token = words[3].Split(':', ',')[1].Substring(1, words[3].Split(':', ',')[1].Length - 2);
            requesttoken.secret = words[4].Split(':', ',')[1].Substring(1, words[4].Split(':', ',')[1].Length - 2);
            requesttoken.verifier = words[5].Split(':')[1].Substring(1, words[5].Split(':', ',')[1].Length - 3);
            try
            {
                accesstoken.token = words[8].Split(':', ',')[1].Substring(1, words[8].Split(':', ',')[1].Length - 2);
                accesstoken.secret = words[9].Split(':', ',')[1].Substring(1, words[9].Split(':', ',')[1].Length - 2);
                accesstoken.endpoint = words[10].Split(':')[1].Substring(1, words[10].Split(':', ',')[1].Length - 1) + ":" + words[10].Split(':')[2] + ":" + words[10].Split(':')[3].Substring(0, words[10].Split(':', ',')[3].Length - 2);
                tokencache.accesstoken = accesstoken;
            }
            catch
            {
                tokencache.accesstoken = null;
            }
            tokencache.requesttoken = requesttoken;
            token._ = tokencache;
            return token;
        }
    }
}    





