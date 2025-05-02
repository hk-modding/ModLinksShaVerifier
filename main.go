package main

import (
	"bufio"
	"crypto/sha256"
	"encoding/hex"
	"encoding/xml"
	"fmt"
	"io"
	"log"
	"net/http"
	"os"
	"strings"
	"time"
)

func main() {
	start := time.Now()

	args := os.Args

	if len(args) < 3 {
		fmt.Println("Usage: ./ModlinksShaVerifier <currentPath> <incomingPath>")
		return
	}

	currentPath := args[1]
	currentFile, err := os.Open(currentPath)
	if err != nil {
		fmt.Println("Error opening current file: ", err)
		return
	}
	defer currentFile.Close()

	incomingPath := args[2]
	incomingFile, err := os.Open(incomingPath)
	if err != nil {
		fmt.Println("Error opening incoming file: ", err)
		return
	}
	defer incomingFile.Close()

	currentReader := bufio.NewReader(currentFile)
	incomingReader := bufio.NewReader(incomingFile)

	var currentModlinks Modlinks
	var incomingModlinks Modlinks

	err = xml.NewDecoder(currentReader).Decode(&currentModlinks)
	if err != nil {
		fmt.Println("Error decoding current file: ", err)
		return
	}

	err = xml.NewDecoder(incomingReader).Decode(&incomingModlinks)
	if err != nil {
		fmt.Println("Error decoding incoming file: ", err)
		return
	}

	mainChannel := make(chan bool)

	checkedManifests := make(map[string]Manifest)
	for _, currentManifest := range currentModlinks.Manifests {
		trimManifest(&currentManifest)
		checkedManifests[currentManifest.Name] = currentManifest
	}

	var checkManifestCount int
	for _, incomingManifest := range incomingModlinks.Manifests {
		trimManifest(&incomingManifest)
		if checkedManifest, exists := checkedManifests[incomingManifest.Name]; exists {
			if checkedManifest != incomingManifest {
				go checkManifest(incomingManifest, mainChannel)
				checkManifestCount++
			}
		} else {
			go checkManifest(incomingManifest, mainChannel)
			checkManifestCount++
		}
	}

	var resultCount int
	for result := range mainChannel {
		if !result {
			log.Fatal("Not all checks were satisfied.")
		} else if resultCount >= checkManifestCount {
			break
		}
		resultCount++
	}

	fmt.Printf("Checked %d mods in %dms\n", resultCount, time.Since(start).Milliseconds())
}

func trimManifest(manifest *Manifest) {
	if manifest.Link != (Link{}) {
		manifest.Link.URL = strings.TrimSpace(manifest.Link.URL)
	} else if manifest.Links != (Links{}) {
		if manifest.Links.Linux != (Link{}) {
			manifest.Links.Linux.URL = strings.TrimSpace(manifest.Links.Linux.URL)
		}
		if manifest.Links.Mac != (Link{}) {
			manifest.Links.Mac.URL = strings.TrimSpace(manifest.Links.Mac.URL)
		}
		if manifest.Links.Windows != (Link{}) {
			manifest.Links.Windows.URL = strings.TrimSpace(manifest.Links.Windows.URL)
		}
	}
}

func checkManifest(manifest Manifest, channel chan bool) {
	fmt.Printf("Checking '%s'\n", manifest.Name)

	if manifest.Link != (Link{}) {
		go checkLink(manifest.Name, manifest.Link, channel)
	} else if manifest.Links != (Links{}) {
		links := manifest.Links

		if links.Linux != (Link{}) {
			go checkLink(manifest.Name, links.Linux, channel)
		}
		if links.Mac != (Link{}) {
			go checkLink(manifest.Name, links.Mac, channel)
		}
		if links.Windows != (Link{}) {
			go checkLink(manifest.Name, links.Windows, channel)
		}
	} else {
		log.Fatalf("No links found for manifest '%s'\n", manifest.Name)
	}
}

func checkLink(manifestName string, link Link, channel chan bool) {
	url := strings.TrimSpace(link.URL)
	response, err := http.Get(url)
	if err != nil {
		fmt.Printf("Failed to fetch link at %s: %s\n", url, err)
		channel <- false
		return
	} else if response.StatusCode != 200 {
		fmt.Println("Invalid status code ", response.StatusCode)
		return
	}

	data, err := io.ReadAll(response.Body)
	if err != nil {
		fmt.Println("Error reading response body: ", err)
		return
	}
	defer response.Body.Close()

	var sha = sha256.New()
	sha.Write(data)
	hash := hex.EncodeToString(sha.Sum(nil))

	if strings.EqualFold(hash, link.SHA256) {
		channel <- true
	} else {
		fmt.Printf("Hash mismatch if %s in link %s. Expected value from modlinks: %s, Actual value: %s\n", manifestName, url, link.SHA256, hash)
		channel <- false
	}
}
