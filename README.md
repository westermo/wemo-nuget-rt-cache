# NuGet Read Through Cache

This project came about because of annoyance with third party nuget repositories being down every now and then.
These annoyances have stopped quite a few builds, and I've had enough of that.

This read through cache will call into nuget repositories that you have configured, and cache the responses for
later use.

It is preconfigured with the nuget.org repository (see `appsettings.json` to configure).

You will need to configure the parameter UriBaseForV2Urls with a URL that points to the /v2/ endpoints. See `appsettings.Development.json` for examples.

# Package Cache Directory Structure

The infix and prefix `-lower-`, `lower-` respectively, denotes a lower case string.

```
/cache-root
  /v3                                             # All packages fetched from v3 sources are stored beneath this directory
    /packages
      /<package-lower-id-1>                       # The package ID in lower case
        /source.json                              # A file containing the repository where this package came from
        /versions.json                            # A file containing all known versions of the package
          /<version-1>                            # A directory for a specific version
            /<lower-id>.<lower-version>.nupkg     # The nupkg file for this version
            /<lower-id>.<lower-version>.nuspec    # The nuspec file for this version
          /<version-2>                            # Next version, etc...
            /<lower-id>.<lower-version>.nupkg
            /<lower-id>.<lower-version>.nuspec
          ...
      /<package-lower-id-2>                       # Another package...
        /source.json
        /versions.json
          /<version-1>
            /<lower-id>.<lower-version>.nupkg
            /<lower-id>.<lower-version>.nuspec
          /<version-2>
            /<lower-id>.<lower-version>.nupkg
            /<lower-id>.<lower-version>.nuspec
          ...
      ...
  /v2                                             # All packages fetched from v2 sources are stored beneath this directory
    /packages
      /<package-lower-id-1>                       # The package ID in lower case
        /info.xml                                 # The package information (Atom feed file) from this package
          /<version-1>                            # A directory for a specific version
            /<lower-id>.<lower-version>.nupkg     # The nupkg file for this version
            ...
          ...
```
