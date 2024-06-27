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

using com.google.apps.peltzer.client.api_clients.assets_service_client;
using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using UnityEngine.UI;

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    ///   Establishes the desktop app.
    /// </summary>
    public class DesktopMain : MonoBehaviour
    {
        /// <summary>
        ///  A default string to show as the user's name if the Plus Client fails to get their actual name.
        /// </summary>
        private const string DEFAULT_DISPLAY_NAME = "Blocks User";
        /// <summary>
        ///   Whether the user has signed in.
        /// </summary>
        private bool isSignedIn;
        /// <summary>
        ///   The GameObject for the add reference image button.
        /// </summary>
        private GameObject addReferenceButton;
        /// <summary>
        ///   The GameObject for the sign in button.
        /// </summary>
        private GameObject signInButton;
        /// <summary>
        ///   The GameObject for the menu.
        /// </summary>
        private GameObject menu;
        /// <summary>
        ///   The GameObject for the menu button.
        /// </summary>
        private GameObject menuButton;
        /// <summary>
        ///   The GameObject for the "Your Models" button on the menu.
        /// </summary>
        private GameObject menuYourModelsButton;
        /// <summary>
        ///   The GameObject for the about poly button on the menu.
        /// </summary>
        private GameObject menuAboutPolyButton;
        /// <summary>
        ///   The GameObject for the sign out button on the menu.
        /// </summary>
        private GameObject menuSignOutButton;
        /// <summary>
        ///   The sprite displaying the menu icon which is the user's avatar.
        /// </summary>
        private Sprite defaultMenuIcon;
        /// <summary>
        ///   The image where the users avatar or default icon is shown.
        /// </summary>
        private Image avatarImage;
        /// <summary>
        ///   The text where the users name or "Sign In" is shown.
        /// </summary>
        private Text displayNameOrPrompt;
        /// <summary>
        ///   The default message to sign in. This is replaced with the user display name on signIn.
        /// </summary>
        private string defaultSignInPrompt;

        /// <summary>
        ///  URL user is taken to when selecting 'About Blocks' from menu
        /// </summary>
        private const string ABOUT_BLOCKS_URL = "https://vr.google.com/blocks";

        public void Setup()
        {
            SetupSigninButton();
            SetupReferenceImage();

            SetupMenu();
            // The menu has to be active on start to set it up. Now that it is setup disable it.
            if (menu != null)
            {
                menu.SetActive(false);
            }

            // Relies on menu, must be called after SetupMenu().
            SetupMenuButton();
        }

        /// <summary>
        ///   Setup the sign in button.
        /// </summary>
        private void SetupSigninButton()
        {
            signInButton = transform.Find("Header/User/sign_in").gameObject;

            if (signInButton != null)
            {
                displayNameOrPrompt = signInButton.GetComponent<Text>();
                defaultSignInPrompt = signInButton.GetComponent<Text>().text;

                HoverableButton signInButtonHoverable = signInButton.AddComponent<HoverableButton>();
                signInButtonHoverable.SetOnClickAction(() =>
                {
                    CloseMenu();
                    // OnClick of hoverable button start the authentication process.
                    PeltzerMain.Instance.InvokeMenuAction(MenuAction.SIGN_IN);
                });
            }
        }

        /// <summary>
        ///   Reset the sign in button and updates the menu when a user logs out.
        /// </summary>
        public void SignOut()
        {
            if (signInButton == null)
                return;

            // Replace the users name with the sign in text.
            displayNameOrPrompt.text = defaultSignInPrompt;

            // Replace the users avatar with the default.
            if (menuButton != null)
            {
                avatarImage.sprite = defaultMenuIcon;
                avatarImage.color = new Color(0f, 0f, 0f, 220 / 255f);
            }

            if (signInButton.GetComponent<HoverableButton>() == null)
            {
                // Re-add the hoverable button that was removed when the user signed in.
                HoverableButton signInButtonHoverable = signInButton.AddComponent<HoverableButton>();
                signInButtonHoverable.SetOnClickAction(() =>
                {
                    CloseMenu();
                    // OnClick of hoverable button start the authentication process.
                    PeltzerMain.Instance.InvokeMenuAction(MenuAction.SIGN_IN);
                });
            }
            else
            {
                signInButton.transform.Find("hover").gameObject.SetActive(false);
            }

            // Modify the menu if it is open to not show the sign out button.
            if (menu.activeInHierarchy && menuSignOutButton != null)
            {
                menuSignOutButton.SetActive(false);
            }

            isSignedIn = false;
        }

        /// <summary>
        ///   Hides the sign in button and updates the menu when the user logs in.
        /// </summary>
        /// <param name="avatarIcon">The user's avatar to display in the UI.</param>
        /// <param name="displayName">The user's name to display in the UI.</param>
        public void SignIn(Sprite avatarIcon, string displayName)
        {
            if (signInButton != null)
            {
                // Disabled the hover object and delete the HoverableButton component.
                signInButton.transform.Find("hover").gameObject.SetActive(false);
                Destroy(signInButton.GetComponent<HoverableButton>());
            }

            // Modify the menu if it is open to show the sign out button.
            if (menu.activeInHierarchy && menuSignOutButton != null)
            {
                menuSignOutButton.SetActive(true);
            }

            // Change the UI elements to display the user's icon and name.
            displayNameOrPrompt.text = displayName != null ? displayName : DEFAULT_DISPLAY_NAME;
            avatarImage.sprite = avatarIcon != null ? avatarIcon : defaultMenuIcon;
            avatarImage.color = avatarIcon != null ? Color.white : new Color(0f, 0f, 0f, 220 / 255f);

            isSignedIn = true;
        }

        /// <summary>
        ///   Setup the add reference image button.
        /// </summary>
        private void SetupReferenceImage()
        {
            addReferenceButton = transform.Find("Header/AddImage").gameObject;

            if (addReferenceButton != null)
            {
                HoverableButton addReferenceButtonHoverable = addReferenceButton.AddComponent<HoverableButton>();
                addReferenceButtonHoverable.SetOnClickAction(() =>
                {
                    if (PeltzerMain.Instance.GetPreviewController() != null)
                    {
                        // OnClick of hoverable button start open the image dialog.
                        PeltzerMain.Instance.GetPreviewController().SelectPreviewImage();
                        CloseMenu();
                    }
                });
            }
        }

        /// <summary>
        ///   Setup the clickable menu button.
        /// </summary>
        private void SetupMenuButton()
        {
            menuButton = transform.Find("Header/User/avatar_menu").gameObject;


            // Find the default menu icon that is displayed when the user is not signed in.
            if (menuButton != null)
            {
                avatarImage = menuButton.GetComponent<Image>();
                defaultMenuIcon = menuButton.GetComponent<Image>().sprite;
            }

            if (menuButton != null)
            {
                HoverableButton menuButtonHoverable = menuButton.AddComponent<HoverableButton>();
                menuButtonHoverable.SetOnClickAction(() =>
                {
                    // OnClick of hoverable button toggle the menu open and close.
                    ToggleMenu();
                });
            }
        }

        /// <summary>
        ///   Setup the menu.
        /// </summary>
        private void SetupMenu()
        {
            menu = transform.Find("Header/Menu").gameObject;

            // Setup each button on the menu.
            if (menu != null)
            {
                menuYourModelsButton = menu.transform.Find("your_models_button").gameObject;
                if (menuYourModelsButton != null)
                {
                    HoverableButton menuFeedbackHoverable = menuYourModelsButton.AddComponent<HoverableButton>();
                    menuFeedbackHoverable.SetOnClickAction(() =>
                    {
                        // OnClick of Poly Library hoverable button opens the Your Models URL in the web browser.
                        Application.OpenURL(AssetsServiceClient.SaveUrl());
                        CloseMenu();
                    });
                }

                menuAboutPolyButton = menu.transform.Find("about_poly_button").gameObject;
                if (menuAboutPolyButton != null)
                {
                    HoverableButton menuAboutPolyHoverable = menuAboutPolyButton.AddComponent<HoverableButton>();
                    menuAboutPolyHoverable.SetOnClickAction(() =>
                    {
                        // OnClick of Poly Library hoverable button opens the "About Poly" page in the web browser.
                        Application.OpenURL(ABOUT_BLOCKS_URL);
                        CloseMenu();
                    });
                }

                menuSignOutButton = menu.transform.Find("sign_out_button").gameObject;
                if (menuSignOutButton != null)
                {
                    HoverableButton menuSignOutHoverable = menuSignOutButton.AddComponent<HoverableButton>();
                    menuSignOutHoverable.SetOnClickAction(() =>
                    {
                        CloseMenu();
                        // OnClick of sign out hoverable deauthenticate and reset the user name and avatar.
                        PeltzerMain.Instance.InvokeMenuAction(MenuAction.SIGN_OUT);
                    });
                }
            }
        }

        /// <summary>
        ///   Closes the menu.
        /// </summary>
        public void CloseMenu()
        {
            if (menu == null)
            {
                return;
            }

            if (menu.activeInHierarchy)
            {
                menu.SetActive(false);
            }
        }

        /// <summary>
        ///   Opens and closes the menu.
        /// </summary>
        private void ToggleMenu()
        {
            if (menu == null)
            {
                return;
            }

            // Close the menu.
            if (menu.activeInHierarchy)
            {
                menu.SetActive(false);
            }
            else
            {
                menu.SetActive(true);
                // Show the sign out button if the user is signed in.
                menuSignOutButton.SetActive(isSignedIn);
            }
        }
    }
}
