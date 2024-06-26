# Blocks README

Blocks is licensed under Apache 2.0. It is not an officially supported
Google product. See the [LICENSE](LICENSE) file for more details.

## Trademarks

The Blocks trademark and logo (“Blocks Trademarks”) are trademarks of
Google, and are treated separately from the copyright or patent license grants
contained in the Apache-licensed Blocks repositories on GitHub. Any use of
the Blocks Trademarks other than those permitted in these guidelines must be
approved in advance.

For more information, read the
[Blocks Brand Guidelines](BRAND_GUIDELINES.md).

## Building the application

Get the Blocks open-source application running on your own devices.

### Prerequisites #TODO

*   [Unity 2018.4.11f1](unityhub://2018.4.11f1/7098af2f11ea)
*   [SteamVR](https://store.steampowered.com/app/250820/SteamVR/)

The code is provided as-is.  Some external dependencies were removed.  It will
not build out of the box.  Someone who is comfortable with Unity and SteamVR
will likely be able to get it running in an afternoon (maybe with some
functionality disabled).

### Changing the application name

_Blocks_ is a Google trademark. If you intend to publish a cloned version of
the application, you are required to choose a different name to distinguish it
from the official version. Before building the application, go into `App.cs` and
the Player settings to change the company and application names to your own.

Please see the [Blocks Brand Guidelines](BRAND_GUIDELINES.md) for more details.

## Systems that were replaced or removed when open-sourcing Blocks

Some systems in Blocks were removed or replaced with alternatives due to
open-source licensing issues. These are:

 * OpenVR
 * AnimatedGifEncoder32
 * LZWEncoder

## Google service API support

Set up Google API support to access Google services in the app.

### Enabling Google service APIs

Follow these steps when enabling Google service APIs:

1.  Create a new project in the
    [Google Cloud Console](https://console.developers.google.com/).
1.  Enable the following APIs and services:

    *   **Google Drive API** — for backup to Google Drive
    *   **People API** — for username and profile picture

Note: The name of your application on the developer console should match the
name you've given the app in `App.kGoogleServicesAppName` in `App.cs`.

### Creating a Google API key

Follow these steps when creating a Google API key:

1.  Go to the Credentials page from the Google Cloud Console.
1.  Click **Create Credential** and select **API key** from the drop-down menu.

### Google OAuth consent screen information

The OAuth consent screen asks users for permission to access their Google
account. You should be able to configure it from the Credentials screen.

Follow these steps when configuring the OAuth consent screen:

1.  Fill in the name and logo of your app, as well as the scope of the user data
    that the app will access.
1.  Add the following paths to the list of scopes:

    *   Google Drive API `../auth/drive.appdata`
    *   Google Drive API `../auth/drive.file`

### Creating an OAuth credential

The credential identifies the application to the Google servers. Follow these
steps to create an OAuth credential:

1.  Create a new credential on the Credentials screen.
1.  Select **OAuth**, and then select **Other**. Take note of the client ID and
    client secret values that are created for you. Keep the client secret a
    secret!

### Storing the Google API Key and credential data

Follow these steps to store the Google API Key and credential data: #TODO

1.  There is an asset in the `Assets/` directory called `Secrets` that contains
    a `Secrets` field. Add a new item to this field.
2.  Select `Google` as the service. Paste in the API key, client ID, and client
    secret that were generated earlier.