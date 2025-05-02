package main

type Modlinks struct {
	Manifests []Manifest `xml:"Manifest"`
}

type Manifest struct {
	Name  string `xml:"Name"`
	Link  Link   `xml:"Link"`
	Links Links  `xml:"Links"`
}

type Links struct {
	Linux   Link `xml:"Linux"`
	Mac     Link `xml:"Mac"`
	Windows Link `xml:"Windows"`
}

type Link struct {
	SHA256 string `xml:"SHA256,attr"`
	URL    string `xml:",cdata"`
}
