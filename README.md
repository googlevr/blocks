# Blocks README

Blocks is licensed under Apache 2.0. It is not an officially supported
Google product. See the [LICENSE](LICENSE) file for more details.

This repo is archived, but a list of active forks is available at
https://github.com/googlevr/blocks/network

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

### Prerequisites

*   [Unity 2019.4.25f1](unityhub://2019.4.25f1/01a0494af254)

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

## Known issues

OculusVR mode and reference image insertion are not currently functional in this
branch.

## Google service API support

Legacy code is included to connect to Google APIs for People and Drive 
integrations. This is not critical to the Blocks experience, but is left
as a convenience for any forks that wish to make use of it with a new backend. 

You must register new projects and obtain new keys and credentials from the 
Google Cloud Console to make use of these features.
