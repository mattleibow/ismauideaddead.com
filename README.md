# ismauideaddead.net
A tracking site for the ismauidead.net site

This is a simple Blazor WebAssembly application that monitors the status of [ismauidead.net](https://ismauidead.net) and reports whether the site is up or down.

## Features

- **Real-time Status Checking**: Monitors ismauidead.net and displays current status
- **Modern UI**: Clean, responsive interface with loading states
- **Direct Link**: Quick access to visit ismauidead.net
- **Client-side Only**: No server required - runs entirely in the browser
- **GitHub Pages Deployment**: Automatically deployed via GitHub Actions

## Technical Details

- Built with .NET 9 Blazor WebAssembly
- No server-side components required
- Responsive design with CSS animations
- CORS-aware status checking with graceful error handling

## Deployment

The site is automatically deployed to GitHub Pages when changes are pushed to the main branch. The GitHub Actions workflow handles building and publishing the WebAssembly artifacts.

## Development

To run locally:

```bash
cd IsMauiDeadDead
dotnet run
```

To build for production:

```bash
cd IsMauiDeadDead
dotnet publish --configuration Release
```

## Note on CORS

Due to browser security restrictions (CORS), the client-side status checking may not work for all external sites. If this occurs, the app will display an appropriate message and users can manually check the site via the provided link.
