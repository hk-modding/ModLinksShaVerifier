#include <chrono>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <mutex>
#include <string>
#include <thread>
#include <vector>
#include <cryptopp/sha.h>
#include <cryptopp/hex.h>
#include <cryptopp/files.h>
#include <Windows.h>
#include "pugiXML/pugixml.hpp"
#pragma comment(lib, "urlmon.lib")

const std::string const dirName = "Cache";

bool AtLeast1Error = false;

std::mutex outputMutex;

void makeDir()
{
	outputMutex.lock();
	std::cout << "Creating directory '" << dirName << "'..." << std::endl;
	outputMutex.unlock();
	std::error_code mkDirErrorCode;
	mkDirErrorCode.clear();
	bool directoryWasCreated = std::filesystem::create_directory(dirName, mkDirErrorCode);
	if (!directoryWasCreated && mkDirErrorCode)
	{
		outputMutex.lock();
		std::cerr << "::error title=Startup::Error creating temporary directory!" << std::endl;
		outputMutex.unlock();
		exit(2);
	}
}

void removeDir()
{
	outputMutex.lock();
	std::cout << "Removing directory '" << dirName << "'" << std::endl;
	outputMutex.unlock();
	std::filesystem::remove(dirName);
}

std::string calcShaOfFile(const std::string filepath)
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

std::string getShaFromUrl(const std::string modName, const std::string urlDownload)
{
	std::string saveTo = dirName + "\\" + modName + ".zip";
	outputMutex.lock();
	std::cout << "Downloading '" << modName << "' '" << urlDownload << "'..." << std::endl;
	outputMutex.unlock();
	auto result = URLDownloadToFileA(NULL, urlDownload.c_str(), saveTo.c_str(), 0, NULL);
	if (result != 0)
	{
		outputMutex.lock();
		std::cerr << "::error title=Download::Error downloading '" << modName << "' '" << urlDownload << "'!" << std::endl;
		outputMutex.unlock();
		return "";
	}

	outputMutex.lock();
	std::cout << "Calculating hash of file for '" << modName << "'..." << std::endl;
	outputMutex.unlock();
	std::string sha = calcShaOfFile(saveTo);

	outputMutex.lock();
	std::cout << "Removing temporary file for '" << modName << "'..." << std::endl;
	outputMutex.unlock();
	std::filesystem::remove(saveTo);
	return sha;
}

void checkShaEntry(const std::string modName, const std::string urlDownload, const std::string expectedSha)
{
	outputMutex.lock();
	std::cout << "Checking entry '" << modName << "'..." << std::endl;
	outputMutex.unlock();

	std::string sha = getShaFromUrl(modName, urlDownload);
	std::string expectedShaLower = expectedSha;
	std::transform(expectedShaLower.begin(), expectedShaLower.end(), expectedShaLower.begin(), ::tolower);

	bool compareResult = sha.compare(expectedShaLower) == 0;
	if (!compareResult)
	{
		outputMutex.lock();
		std::cerr << "::error title=Check::Hash mismatch with '" << modName << "'"
			<< ". Expected: " << expectedShaLower
			<< ", Downloaded: " << sha << "!" << std::endl;
		outputMutex.unlock();
		AtLeast1Error = true;
	}

}

int main(int argc, char* argv[])
{
	std::chrono::high_resolution_clock::time_point start = std::chrono::high_resolution_clock::now();
	if (argc < 2)
	{
		outputMutex.lock();
		std::cerr << "::error title=Startup::call like `.\\ModLinksShaVerifier.exe path_to_xml_file`!" << std::endl;
		outputMutex.unlock();
		exit(1);
	}
	makeDir();

	pugi::xml_document doc;
	outputMutex.lock();
	std::cout << "Loading XML file..." << std::endl;
	outputMutex.unlock();
	pugi::xml_parse_result result = doc.load_file(argv[1]);
	if (result.status != pugi::status_ok)
	{
		outputMutex.lock();
		std::cerr << "::error title=Startup::Error loading XML file!" << std::endl;
		outputMutex.unlock();
		removeDir();
		exit(3);
	}
	std::vector<std::thread> threads;
	pugi::xml_node rootNode = doc.first_child();
	outputMutex.lock();
	std::cout << "Starting the checks..." << std::endl;
	outputMutex.unlock();
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
			std::transform(expectedSha.begin(), expectedSha.end(), expectedSha.begin(), ::tolower);
			threads.push_back(std::thread(checkShaEntry, reportName, downloadUrl, expectedSha));
		}
	}
	for (int i = 0; i < threads.size(); i++)
	{
		threads[i].join();
	}
	if (!AtLeast1Error)
	{
		outputMutex.lock();
		std::cout << "No mismatches to report!" << std::endl;
		outputMutex.unlock();
	}

	removeDir();
	std::chrono::high_resolution_clock::time_point end = std::chrono::high_resolution_clock::now();
	outputMutex.lock();
	std::cout << "The entire operation took " << std::chrono::duration_cast<std::chrono::seconds>(end - start).count() << " seconds!" << std::endl;
	outputMutex.unlock();
	return 0;
}
