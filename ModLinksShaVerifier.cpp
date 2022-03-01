#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>
#include <cryptopp/sha.h>
#include <cryptopp/hex.h>
#include <cryptopp/files.h>
#include <Windows.h>
#include "pugiXML/pugixml.hpp"
#pragma comment(lib, "urlmon.lib")

std::string calcShaOfFile(std::string filepath)
{
	// Crypto++ SHA256 object
	CryptoPP::SHA256 hash;

	// Our output
	std::string output;

	// Crypto++ Filter the hash to output string
	CryptoPP::HashFilter filter(hash, new CryptoPP::HexEncoder(new CryptoPP::StringSink(output)));

	// Crypto++ file source
	CryptoPP::FileSource fileSource(filepath.c_str(), true, new CryptoPP::Redirector(filter));

	// Convert to lowercase
	std::transform(output.begin(), output.end(), output.begin(), ::tolower);
	return output;
}

bool checkShaEntry(const std::string urlDownload, const std::string expectedSha)
{
	std::string saveTo = "Cache\\tmpfile.zip";
	std::cout << "Downloading '" << urlDownload << "'..." << std::endl;
	auto result = URLDownloadToFileA(NULL, urlDownload.c_str(), saveTo.c_str(), 0, NULL);
	if (result != 0)
	{
		std::cerr << "Error downloading file!" << std::endl;
		exit(4);
	}

	std::cout << "Calculating hash of file..." << std::endl;
	std::string sha = calcShaOfFile(saveTo);
	std::string expectedShaLower = expectedSha;
	std::transform(expectedShaLower.begin(), expectedShaLower.end(), expectedShaLower.begin(), ::tolower);

	std::cout << "Removing temporary file..." << std::endl;
	std::filesystem::remove(saveTo);
	return sha.compare(expectedShaLower) == 0;
}

int main(int argc, char* argv[])
{
	if (argc < 2)
	{
		std::cerr << "ModLinksShaVerifier path_to_xml_file" << std::endl;
		exit(1);
	}
	std::cout << "Creating directory 'Cache'..." << std::endl;
	std::error_code mkDirErrorCode;
	mkDirErrorCode.clear();
	bool directoryWasCreated = std::filesystem::create_directory("Cache", mkDirErrorCode);
	if (!directoryWasCreated && mkDirErrorCode)
	{
		std::cerr << "Error creating temporary directory!" << std::endl;
		exit(2);
	}

	pugi::xml_document doc;
	std::cout << "Loading XML file..." << std::endl;
	pugi::xml_parse_result result = doc.load_file(argv[1]);
	if (result.status != pugi::status_ok)
	{
		std::cerr << "Error loading XML file!" << std::endl;
		exit(3);
	}
	bool gotAtLeast1Error = false;
	pugi::xml_node rootNode = doc.first_child();
	for (pugi::xml_node manifestNode : rootNode)
	{
		std::string manifestName;
		std::vector<pugi::xml_node> manifestLinks;
		for (pugi::xml_node childNode : manifestNode)
		{
			if (std::string(childNode.name()).compare("Name") == 0)
			{
				manifestName = std::string(childNode.text().as_string());
			}
			else if (std::string(childNode.name()).compare("Link") == 0)
			{
				manifestLinks.push_back(childNode);
			}
			else if (std::string(childNode.name()).compare("Links") == 0)
			{
				for (pugi::xml_node linkNode : childNode)
				{
					manifestLinks.push_back(linkNode);
				}
			}
		}
		if (std::string(rootNode.name()).compare("ApiLinks") == 0)
		{
			manifestName = "Modding API";
		}
		for (pugi::xml_node linkNode : manifestLinks)
		{
			std::string reportName = manifestName;
			if (std::string(linkNode.name()).compare("Link") != 0)
			{
				// linux, mac or windows
				reportName += " (";
				reportName += std::string(linkNode.name());
				reportName += ")";
			}
			std::string downloadUrl(linkNode.text().as_string());
			std::string expectedSha(linkNode.attribute("SHA256").as_string());
			std::cout << "Checking entry '" << reportName << "'..." << std::endl;
			bool shaCheckResult = checkShaEntry(downloadUrl, expectedSha);
			if (!shaCheckResult)
			{
				std::cerr << "SHA256 of '" << reportName << "' does not match with modlinks!" << std::endl;
				gotAtLeast1Error = true;
			}
		}
	}
	if (!gotAtLeast1Error)
	{
		std::cout << "No mismatches to report!" << std::endl;
	}

	std::cout << "Removing directory 'Cache'" << std::endl;
	std::filesystem::remove("Cache");
	return gotAtLeast1Error ? 5 : 0;
}
