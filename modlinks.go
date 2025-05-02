package main

// Represents an entire Modlinks XML document.
type Modlinks struct {
	// An array of all manifests within the Modlinks document.
	Manifests []Manifest `xml:"Manifest"`
}

// Represents a single mod manifest within a Modlinks document.
type Manifest struct {
	// The name of the mod manifest.
	Name string `xml:"Name"`
	// A single link representing a cross-platform mod.
	Link Link `xml:"Link"`
	// A group of links representing each mod for the Linux, macOS, and Windows platform.
	Links Links `xml:"Links"`
}

// Represents a links structure in a mod manifest with Linux, macOS, and Windows links.
type Links struct {
	// The link to the Linux build.
	Linux Link `xml:"Linux"`
	// The link to the macOS build.
	Mac Link `xml:"Mac"`
	// The link to the Windows build.
	Windows Link `xml:"Windows"`
}

// Represents a link structure in a mod manifest.
type Link struct {
	// The SHA256 hash proposed by the link tag.
	SHA256 string `xml:"SHA256,attr"`
	// The URL to the shared .NET library or zip file whose SHA256 hash will be verified.
	URL string `xml:",cdata"`
}
