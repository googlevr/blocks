// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

using Newtonsoft.Json.Linq;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.api_clients.assets_service_client;

namespace com.google.apps.peltzer.client.entitlement {

  /// Handle accessing OAuth2 based web services. There are known issues with non-square avatars.

  public class OAuth2Identity : MonoBehaviour {
    public class UserInfo {
      public string id;
      public string name;
      public string email;
      public string location;
      public Sprite icon;
    }

    private const string m_ServiceName = "Google";
    private const string m_ClientId = "TODO";
    private const string m_ClientSecret = "TODO";
    private const string m_RequestTokenUri = "https://accounts.google.com/o/oauth2/auth";
    private const string m_AccessTokenUri = "https://accounts.google.com/o/oauth2/token";
    private const string m_UserInfoUri = "[Removed]";
    private const string m_OAuthScope = "profile email " + 
      "https://www.googleapis.com/auth/plus.me " +
      "https://www.googleapis.com/auth/plus.peopleapi.readwrite";
    private const string m_CallbackPath = "/callback";
    private const string m_ReplaceHeadset = "ReplaceHeadset";
    private string m_CallbackFailedMessage = "Sorry!";

    // User avatar pixel density. This is the number of pixels that correspond to one unit in world space.
    // Larger values will make a smaller (more dense) avatar. Smaller values will make it larger (less dense).
    private const int USER_AVATAR_PIXELS_PER_UNIT = 30;

    private static Color UI_BACKGROUND_COLOR = Color.clear;

    public static OAuth2Identity Instance;
    public static event Action OnProfileUpdated {
      add {
        if (Instance != null) {
          value();  // Call the event once for the current profile.
        }
        m_OnProfileUpdated += value;
      }
      remove {
        m_OnProfileUpdated -= value;
      }
    }

    private static event Action m_OnProfileUpdated;
    private static string PLAYER_PREF_REFRESH_KEY_SUFFIX = "BlocksOAuthRefreshKey";
    private string m_PlayerPrefRefreshKey;
    private const string kIconSizeSuffix = "?sz=128";

    private string m_AccessToken;
    private string m_RefreshToken;
    private UserInfo m_User = null;

    private HttpListener m_HttpListener;
    private int m_HttpPort;
    private bool m_WaitingOnAuthorization;
    private string m_VerificationCode;
    private Boolean m_VerificationError;

    public UserInfo Profile {
      get { return m_User; }
      set {
        m_User = value;
        if (m_OnProfileUpdated != null) {
          m_OnProfileUpdated();
        }
      }
    }

    public bool LoggedIn {
      // We don't consider us logged in until we have the UserInfo
      get { return m_RefreshToken != null && Profile != null; }
    }

    public bool HasAccessToken {
      get { return m_AccessToken != null; }
    }

    void Awake() {
      Instance = this;
      m_PlayerPrefRefreshKey = String.Format("{0}{1}", m_ServiceName, PLAYER_PREF_REFRESH_KEY_SUFFIX);

      if (PlayerPrefs.HasKey(m_PlayerPrefRefreshKey)) {
        m_RefreshToken = PlayerPrefs.GetString(m_PlayerPrefRefreshKey);
      }
    }

    // Use Google Account Chooser to open a url with the current account.
    public void OpenURL(string url) {
      if (LoggedIn) {
        url = string.Format("https://accounts.google.com/AccountChooser?Email={0}&continue={1}",
          Profile.email, url);
      }
      Application.OpenURL(url);
    }

    public void Login(System.Action onSuccess, System.Action onFailure, bool promptUserIfNoToken) {
      StartCoroutine(Authorize(onSuccess, onFailure, promptUserIfNoToken));
    }

    public void Logout() {
      if (m_RefreshToken != null) {
        // Not sure if it's possible for m_User to be null here.
        if (Profile != null) {
          Debug.Log(Profile.name + " logged out.");
        } else {
          Debug.Log("Logged out.");
        }
        m_RefreshToken = null;
        m_AccessToken = null;
        Profile = null;
        PlayerPrefs.DeleteKey(m_PlayerPrefRefreshKey);
      }
    }

    /// Sign an outgoing request.
    public void Authenticate(UnityWebRequest www) {
      www.SetRequestHeader("Authorization", String.Format("Bearer {0}", m_AccessToken));
    }

    private static string UserInfoRequestUri() {
      return String.Format("{0}&key={1}", m_UserInfoUri, AssetsServiceClient.POLY_KEY);
    }

    private IEnumerator GetUserInfo() {
      if (String.IsNullOrEmpty(m_RefreshToken)) {
        yield break;
      }

      UserInfo user = new UserInfo();
      for (int i = 0; i < 2; i++) {
        using (UnityWebRequest www = UnityWebRequest.Get(UserInfoRequestUri())) {
          Authenticate(www);
          yield return www.Send();
          if (www.responseCode == 200) {
            JObject json = JObject.Parse(www.downloadHandler.text);
            user.id = json["resourceName"].ToString();
            user.name = json["names"][0]["displayName"].ToString();
            string iconUri = json["photos"][0]["url"].ToString();
            if (json["residences"] != null) {
              user.location = json["residences"][0]["value"].ToString();
            }
            if (json["emailAddresses"] != null) {
              foreach (var email in json["emailAddresses"]) {
                var primary = email["metadata"]["primary"];
                if (primary != null && primary.Value<bool>()) {
                  user.email = email["value"].ToString();
                  break;
                }
              }
            }
            Profile = user;
            yield return LoadProfileIcon(iconUri);

            Debug.Log(Profile.name + " logged in.");
            yield break;
          } else if (www.responseCode == 401) {
            yield return Reauthorize();
          } else {
            Debug.Log(www.responseCode);
            Debug.Log(www.error);
            Debug.Log(www.downloadHandler.text);
          }
        }
      }
      Profile = null;
    }

    // I have a refresh token, I need an access token.
    public IEnumerator<object> Reauthorize() {
      m_AccessToken = null;
      if (!String.IsNullOrEmpty(m_RefreshToken)) {
        Dictionary<string, string> parameters = new Dictionary<string, string>();
        parameters.Add("client_id", m_ClientId);
        parameters.Add("client_secret", m_ClientSecret);
        parameters.Add("refresh_token", m_RefreshToken);
        parameters.Add("grant_type", "refresh_token");
        using (UnityWebRequest www = UnityWebRequest.Post(m_AccessTokenUri, parameters)) {
          yield return www.Send();
          if (www.isNetworkError) {
            Debug.LogError("Network error");
            yield break;
          }

          if (www.responseCode == 400 || www.responseCode == 401) {
            // Refresh token revoked or expired - forget it
            m_RefreshToken = null;
            PlayerPrefs.DeleteKey(m_PlayerPrefRefreshKey);
          } else {
            JObject json = JObject.Parse(www.downloadHandler.text);
            m_AccessToken = json["access_token"].ToString();
          }
        }
      }
    }

    /// <summary>
    ///   Attempt to authorise the user via a refresh token, or by giving them a browser window 
    ///   to authorize permissions then get refresh and access tokens.
    /// </summary>
    /// <param name="onSuccess">Callback on success</param>
    /// <param name="onFailure">Callback on failure</param>
    /// <param name="promptUserIfNoToken">
    ///   If true, will prompt the user to sign in via a browser if no refresh token found.
    /// </param>
    public IEnumerator<object> Authorize(System.Action onSuccess, System.Action onFailure, bool promptUserIfNoToken) {
      if (String.IsNullOrEmpty(m_RefreshToken) && promptUserIfNoToken) {
        int port = m_HttpPort != 0 ? m_HttpPort : StartHttpListener();
        string redirectURI = string.Format("http://localhost:{0}/", port);

        if (port == 0) {
          // Failed to start HTTP server.
          onFailure();
          yield break;
        }

        StringBuilder sb = new StringBuilder(m_RequestTokenUri)
            .Append("?client_id=").Append(Uri.EscapeDataString(m_ClientId))
            .Append("&redirect_uri=").Append("http://localhost:").Append(port).Append(m_CallbackPath)
            .Append("&response_type=code")
            .Append("&scope=").Append(m_OAuthScope);

        // Something about the url makes OpenURL() not work on OSX, so use a workaround
        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer) {
          System.Diagnostics.Process.Start(sb.ToString());
        } else {
          Application.OpenURL(sb.ToString());
        }

        if (m_WaitingOnAuthorization) {
          // A previous attempt is already waiting
          yield break;
        }
        m_WaitingOnAuthorization = true;
        m_VerificationCode = null;
        m_VerificationError = false;

        // Wait for verification
        while (m_VerificationCode == null || m_VerificationError) {
          yield return null;
        }

        if (m_VerificationError) {
          Debug.LogError("Account verification failed");
          Debug.LogFormat("Verification error {0}", m_VerificationCode);
          m_WaitingOnAuthorization = false;
          yield break;
        }

        // Exchange for tokens
        var parameters = new Dictionary<string, string>();
        parameters.Add("code", m_VerificationCode);
        parameters.Add("client_id", m_ClientId);
        parameters.Add("client_secret", m_ClientSecret);
        parameters.Add("redirect_uri", String.Format("http://localhost:{0}{1}", port, m_CallbackPath));
        parameters.Add("grant_type", "authorization_code");

        UnityWebRequest www = UnityWebRequest.Post(m_AccessTokenUri, parameters);

        yield return www.Send();
        if (www.isNetworkError) {
          Debug.LogError("Network error");
          m_WaitingOnAuthorization = false;
          yield break;
        } else if (www.responseCode >= 400) {
          Debug.LogError("Authorization failed");
          Debug.LogFormat("Authorization error {0}", www.downloadHandler.text);
          m_WaitingOnAuthorization = false;
          yield break;
        }

        JObject json = JObject.Parse(www.downloadHandler.text);
        if (json != null) {
          m_AccessToken = json["access_token"].ToString();
          m_RefreshToken = json["refresh_token"].ToString();
          PlayerPrefs.SetString(m_PlayerPrefRefreshKey, m_RefreshToken);
        }
        m_WaitingOnAuthorization = false;
      }

      yield return GetUserInfo();

      if (LoggedIn) {
        onSuccess();
        PeltzerMain.Instance.Analytics.UserAuthenticated();
      } else {
        onFailure();
      }
    }

    private int StartHttpListener() {
      // Get a free port
      TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
      tcpListener.Start();
      m_HttpPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
      tcpListener.Stop();

      // There is a small possibility of someone else grabbing the port here, but I'm not aware
      // of any other way to do this with HttpListener.

      // We can't load resources in the listener so do it here.
      string responseText = Resources.Load<TextAsset>("Text/headset").text;

      try {
        m_HttpListener = new HttpListener();
        m_HttpListener.Prefixes.Add(String.Format("http://localhost:{0}/", m_HttpPort));
        m_HttpListener.Start();
        ThreadPool.QueueUserWorkItem((o) => {
          while (m_HttpListener.IsListening) {
            ThreadPool.QueueUserWorkItem((c) => {
              var ctx = c as HttpListenerContext;
              try {
                string response = HttpRequestCallback(ctx.Request, responseText);
                byte[] buf = System.Text.Encoding.UTF8.GetBytes(response);
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
              } finally {
                ctx.Response.Close();
              }
            }, m_HttpListener.GetContext());
          }
        });
      } catch (System.Net.Sockets.SocketException e) {
        Debug.LogFormat("HttpListener failed to start\n{0}", e);
        m_HttpListener = null;
        m_HttpPort = 0;
      }

      return m_HttpPort;
    }

    private void StopHttpListener() {
      if (m_HttpListener != null) {
        m_HttpListener.Abort();
        m_HttpListener = null;
        m_HttpPort = 0;
      }
    }

    private string HttpRequestCallback(HttpListenerRequest request, string message) {
      if (request.Url.AbsolutePath == m_CallbackPath) {
        if (request.Url.Query.StartsWith("?code=")) {
          m_VerificationCode = request.Url.Query.Substring(6);
          m_VerificationError = false;
        } else if (request.Url.Query.StartsWith("#error=")) {
          m_VerificationError = true;
          m_VerificationCode = request.Url.Query.Substring(7);
        } else {
          m_VerificationError = true;
          m_VerificationCode = null;
        }
      }
      return m_VerificationError ? m_CallbackFailedMessage : message;
    }

    // Unity doesn't do this correctly so we do it ourselves
    // https://fogbugz.unity3d.com/default.asp?846309_0391asaijk4j3vnt
    static public byte[] serializeMultipartForm(string filepath, byte[] boundary, string contentType) {
      Debug.Assert(File.Exists(filepath), filepath);
      FileInfo fileInfo = new FileInfo(filepath);
      long filesize = fileInfo.Length;

      FileStream stream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
      byte[] buffer = new byte[filesize];
      stream.Read(buffer, 0, (int)filesize);
      stream.Close();
      return serializeMultipartForm(buffer, boundary, Path.GetFileName(filepath), contentType);
    }

    static public byte[] serializeMultipartForm(byte[] buffer, byte[] boundary, string filename, string contentType) {
      MemoryStream ms = new MemoryStream();

      const byte dash = 0x2d;
      ms.WriteByte(dash);
      ms.WriteByte(dash);
      ms.Write(boundary, 0, boundary.Length);
      string header = String.Format("\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{0}\"\r\nContent-Type: {1}\r\n\r\n",
        filename, contentType);
      byte[] headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
      ms.Write(headerBytes, 0, headerBytes.Length);

      ms.Write(buffer, 0, buffer.Length);

      ms.WriteByte(0x0d);
      ms.WriteByte(0x0a);
      ms.WriteByte(dash);
      ms.WriteByte(dash);
      ms.Write(boundary, 0, boundary.Length);
      ms.WriteByte(0x0d);
      ms.WriteByte(0x0a);

      return ms.ToArray();
    }

    private IEnumerator LoadProfileIcon(string uri) {
      if (Profile == null) {
        yield break;
      }
      using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(uri + kIconSizeSuffix)) {
        yield return www.Send();
        if (www.isNetworkError || www.responseCode >= 400) {
          Debug.LogErrorFormat("Error downloading {0}, error {1}", uri, www.responseCode);
          Profile.icon = null;
        } else {
          // Convert the texture to a circle and set it as the user's avatar in the UI and the PolyMenu.
          Texture2D profileImage = DownloadHandlerTexture.GetContent(www);
          Profile.icon = Sprite.Create(CropSquareTextureToCircle(profileImage),
            new Rect(0, 0, profileImage.width, profileImage.height), new Vector2(0.5f, 0.5f),
            USER_AVATAR_PIXELS_PER_UNIT);
        }
        if (m_OnProfileUpdated != null) {
          m_OnProfileUpdated();
        }
      }
    }

    /// <summary>
    ///   Gets a free port on the local machine to use for the local redirect HttpListener.
    ///   (see http://stackoverflow.com/a/3978040)
    /// </summary>
    /// <returns>A free port on the local machine.</returns>
    private static int GetRandomUnusedPort() {
      TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      int port = ((IPEndPoint)listener.LocalEndpoint).Port;
      listener.Stop();
      return port;
    }

    private Texture2D CropSquareTextureToCircle(Texture2D squareTexture) {
      float width = squareTexture.width;
      float height = squareTexture.height;
      float radius = width / 2;
      float centerX = squareTexture.width / 2;
      float centerY = squareTexture.height / 2;
      Color[] c = squareTexture.GetPixels(0, 0, (int)width, (int)height);
      Texture2D circleTexture = new Texture2D((int)height, (int)width);
      for (int i = 0; i < height * width; i++) {
        int y = Mathf.FloorToInt(i / width);
        int x = Mathf.FloorToInt(i - (y * width));
        if (radius * radius >= (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY)) {
          circleTexture.SetPixel(x, y, c[i]);
        } else {
          circleTexture.SetPixel(x, y, UI_BACKGROUND_COLOR);
        }
      }
      circleTexture.Apply();
      return circleTexture;
    }
  }
}
