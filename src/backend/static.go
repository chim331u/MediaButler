package main

import (
	"embed"
	"io/fs"
	"net/http"
	"os"
)

//go:embed dist/*
var webAssets embed.FS

// spaFileSystem wrapping http.FileSystem to serve index.html as a fallback
// for client-side routing (single page application support).
type spaFileSystem struct {
	fs http.FileSystem
}

func (sfs spaFileSystem) Open(name string) (http.File, error) {
	f, err := sfs.fs.Open(name)
	if err != nil {
		// If the file is not found, fallback to index.html to allow Svelte client-side routing
		if os.IsNotExist(err) {
			return sfs.fs.Open("index.html")
		}
		return nil, err
	}
	return f, nil
}

// RegisterStaticRoutes mounts the embedded Svelte SPA assets at the root of the mux.
func RegisterStaticRoutes(mux *http.ServeMux) {
	subDist, err := fs.Sub(webAssets, "dist")
	if err != nil {
		panic("embedded static assets directory 'dist' is missing: " + err.Error())
	}

	fileServer := http.FileServer(spaFileSystem{fs: http.FS(subDist)})
	mux.Handle("/", fileServer)
}
