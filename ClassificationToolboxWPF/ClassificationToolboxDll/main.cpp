#pragma once
#include<iostream>
#include<fstream>
#include<vector> 
#include<ppl.h>
#include<omp.h>
#include<filesystem>

//#include "tbb/task_scheduler_init.h"
//#include "tbb/parallel_for.h"
//#include "tbb/blocked_range.h"
//#include "tbb/scalable_allocator.h"

#include"utills.h"
#include"feature.h"
#include"classifier.h"
#include"boosting.h"
#include"cascade.h"

using namespace std;
//using namespace tbb;

typedef void(__stdcall* ProgressCallback)(int);

double** allFeatures;
int* allClasses;
int samplesCount;
int featuresCount;

Classifier* InitializeClassifier(string clsName, string path)
{
	if (clsName == ZeroRule::GetType())
		return new ZeroRule(path);
	else if (clsName == WeakPerceptron::GetType())
		return  new WeakPerceptron(path);
	else if (clsName == DecisionStump::GetType())
		return  new DecisionStump(path);
	else if (clsName == BinnedDecisionStump::GetType())
		return  new BinnedDecisionStump(path);
	else if (clsName == RegularBins::GetType())
		return  new RegularBins(path);
	else if (clsName == BinnedTree::GetType())
		return  new BinnedTree(path);
	else if (clsName == AdaBoost::GetType())
		return  new AdaBoost(path);
	else if (clsName == RealBoost::GetType())
		return  new RealBoost(path);
	else if (clsName == CascadeOfClassifier::GetType())
		return new CascadeOfClassifier(path);
	else if (clsName == GraphCascadeOfClassifier::GetType())
		return new GraphCascadeOfClassifier(path);
	else if (clsName == DijkstraGraphCascadeOfClassifier::GetType())
		return new DijkstraGraphCascadeOfClassifier(path);
	else
		return nullptr;
}

Classifier* InitializeClassifier(string clsName, ClassifierParameters param)
{
	if (clsName == ZeroRule::GetType())
		return new ZeroRule();
	else if (clsName == WeakPerceptron::GetType())
		return  new WeakPerceptron(param);
	else if (clsName == DecisionStump::GetType())
		return  new DecisionStump();
	else if (clsName == BinnedDecisionStump::GetType())
		return  new BinnedDecisionStump(param);
	else if (clsName == RegularBins::GetType())
		return  new RegularBins(param);
	else if (clsName == BinnedTree::GetType())
		return  new BinnedTree(param);
	else if (clsName == AdaBoost::GetType())
		return  new AdaBoost(param);
	else if (clsName == RealBoost::GetType())
		return  new RealBoost(param);
	else if (clsName == CascadeOfClassifier::GetType() && param.isGraph == false)
		return new CascadeOfClassifier(param);
	else if (clsName == GraphCascadeOfClassifier::GetType() && param.isGraph == true && param.isDijkstra == false)
		return new GraphCascadeOfClassifier(param);
	else if (clsName == DijkstraGraphCascadeOfClassifier::GetType() && param.isGraph == true && param.isDijkstra == true)
		return new DijkstraGraphCascadeOfClassifier(param);
	else
		return nullptr;
}

extern "C" __declspec(dllexport) void __stdcall InitializePFMM()
{
	PFMM::initializeExtractor();
}

extern "C" __declspec(dllexport) void __stdcall ClearMemoryPFMM()
{
	PFMM::clearMemory();
}

extern "C" __declspec(dllexport) void __stdcall InitializeZernike(int ompThreads, int bufferSize)
{
	ZernikeII::initializeExtractor();
}

extern "C" __declspec(dllexport) void __stdcall ClearMemoryZernike(int ompThreads, int bufferSize)
{
	ZernikeII::clearMemory();
}

extern "C" __declspec(dllexport) void __stdcall InitializeZernikeFP(int ompThreads, int bufferSize)
{
	ZernikeFPII::initializeExtractor();
}

extern "C" __declspec(dllexport) void __stdcall ClearMemoryZernikeFP(int ompThreads, int bufferSize)
{
	ZernikeFPII::clearMemory();
}

extern "C" __declspec(dllexport) void __stdcall SetParallelity(int ompThreads, int bufferSize)
{
	OMP_NUM_THR = ompThreads;
	PROGRESS_BUFFER = bufferSize;
}

extern "C" __declspec(dllexport) int __stdcall ZernikeComparison(double* resultsII, double* resultsFPII, int* extractorParameters,
	const unsigned char* image, const int bytesPerPixel, const int width, const int height, const int stride, const int winSize, const double thr, ProgressCallback progressCallback)
{
	int errorCode = 0;

	int size = (width - winSize + 1) * (height - winSize + 1);
	int* thr10II = new int[size], *thr10FPII = new int[size], *thr25II = new int[size], *thr25FPII = new int[size], *thr50II = new int[size], *thr50FPII = new int[size];

	ofstream errfile, errfile1, errfile2, errfile3, timesLog;
	string filename = "zernikeComparison_" + to_string(winSize) + "_p" + to_string(extractorParameters[0]) + "_q" + to_string(extractorParameters[1]) + "_r" + to_string(extractorParameters[2]) + "_rt" + to_string(extractorParameters[3]) + "_d" + to_string(extractorParameters[4]);
	errfile.open(filename + "_app_thr.m");
	errfile1.open(filename + "_0_10.m");
	errfile2.open(filename + "_0_25.m");
	errfile3.open(filename + "_0_50.m");
	timesLog.open(filename + "timelogs.txt");


	


	const double* const*&& grayImage = nullptr;
	try
	{
		grayImage = ConvertToGray(image, bytesPerPixel, width, height, stride);

		Zernike zernike(extractorParameters[0], extractorParameters[1], extractorParameters[2], extractorParameters[3], SaveFileType::binary8bit);
		zernike.loadImageData(grayImage, height, width);

		ZernikeII zernikeII(extractorParameters[0], extractorParameters[1], extractorParameters[2], extractorParameters[3], SaveFileType::binary8bit);
		std::chrono::system_clock::time_point beginTimeZernikeII = std::chrono::system_clock::now();
		zernikeII.loadImageData(grayImage, height, width);
		std::chrono::system_clock::time_point endTimeZernikeII = std::chrono::system_clock::now();
		timesLog << "II times: " << std::chrono::duration_cast<std::chrono::milliseconds> (endTimeZernikeII - beginTimeZernikeII).count() << endl;
	
		ZernikeFPII zernikeFPII(extractorParameters[0], extractorParameters[1], extractorParameters[2], extractorParameters[3], extractorParameters[4], SaveFileType::binary8bit);
		std::chrono::system_clock::time_point beginTimeZernikeFPII = std::chrono::system_clock::now();
		zernikeFPII.loadImageData(grayImage, height, width);
		std::chrono::system_clock::time_point endTimeZernikeFPII = std::chrono::system_clock::now();
		timesLog << "FPII times: " << std::chrono::duration_cast<std::chrono::milliseconds> (endTimeZernikeFPII - beginTimeZernikeFPII).count() << endl;

		int id = 0;
		int repetions = 10;
		int scales[] = { 208 };

		int xxx = 0;
		int yyy = 0;
		for (int s = 0; s < 1; s++)
		{
			timesLog << "windows: " << scales[s] << endl;
			long avgDef = 0;
			long avgII = 0;
			long avgFPII = 0;
			for (int i = 0; i < repetions; i++)
			{
				for (int j = 0; j < repetions; j++)
				{
					xxx = i * 5;
					yyy = j * 5;

					std::chrono::system_clock::time_point beginTimeZernikeDef = std::chrono::system_clock::now();
					auto [countNoII, featuresNoII] = zernike.extractFromWindow(scales[s], scales[s], xxx, yyy);
					std::chrono::system_clock::time_point endTimeZernikeDef = std::chrono::system_clock::now();

					std::chrono::system_clock::time_point beginTimeZernikeII = std::chrono::system_clock::now();
					auto [countII, featuresII] = zernikeII.extractFromWindow(scales[s], scales[s], xxx, yyy);
					std::chrono::system_clock::time_point endTimeZernikeII = std::chrono::system_clock::now();

					std::chrono::system_clock::time_point beginTimeZernikeFPII = std::chrono::system_clock::now();
					auto [countFPII, featuresFPII] = zernikeFPII.extractFromWindow(scales[s], scales[s], xxx, yyy);
					std::chrono::system_clock::time_point endTimeZernikeFPII = std::chrono::system_clock::now();

					delete[] featuresNoII;
					delete[] featuresII;
					delete[] featuresFPII;

					avgDef += std::chrono::duration_cast<std::chrono::microseconds> (endTimeZernikeDef - beginTimeZernikeDef).count();
					avgII += std::chrono::duration_cast<std::chrono::microseconds> (endTimeZernikeII - beginTimeZernikeII).count();
					avgFPII += std::chrono::duration_cast<std::chrono::microseconds> (endTimeZernikeFPII - beginTimeZernikeFPII).count();
				}
			}
			timesLog << "Def win avg times: " << avgDef / (repetions*repetions) << endl;
			timesLog << "II win avg times: " << avgII / (repetions * repetions) << endl;
			timesLog << "FPII win avg times: " << avgFPII / (repetions * repetions) << endl;
		}
		timesLog.close();

		
		for (int i = 0; i < width - winSize + 1; i++)
		{
			for (int j = 0; j < height - winSize + 1; j++)
			{
				
				auto [countNoII, featuresNoII] = zernike.extractFromWindow(winSize, winSize, i, j);
				auto [countII, featuresII] = zernikeII.extractFromWindow(winSize, winSize, i, j);
				auto [countFPII, featuresFPII] = zernikeFPII.extractFromWindow(winSize, winSize, i, j);

				resultsII[id] = 0;
				resultsFPII[id] = 0;

				thr10II[id] = 0;
				thr10FPII[id] = 0;
				thr25II[id] = 0;
				thr25FPII[id] = 0;
				thr50II[id] = 0;
				thr50FPII[id] = 0;
				for (int k = 0; k < countNoII; k++)
				{
					//errfile << "fp" << endl;
					double tmp1 = abs(featuresII[k] - featuresNoII[k]) / abs(featuresNoII[k]);
					double tmp2 = abs(featuresFPII[k] - featuresNoII[k]) / abs(featuresNoII[k]);

					if (tmp1 > thr)
						resultsII[id]++;
					if (tmp2 > thr)
						resultsFPII[id]++;

					if (tmp1 > 0.1)
					{
						thr10II[id]++;
						if (tmp1 > 0.25)
						{
							thr25II[id]++;
							if (tmp1 > 0.5)
							{
								thr50II[id]++;
							}
						}
					}
					if (tmp2 > 0.1)
					{
						thr10FPII[id]++;
						if (tmp2 > 0.25)
						{
							thr25FPII[id]++;
							if (tmp2 > 0.5)
							{
								thr50FPII[id]++;
							}
						}
					}
				}
				

				delete[] featuresNoII;
				delete[] featuresII;
				delete[] featuresFPII;

				id++;
			}
			progressCallback(id);
		}

		errfile << "thr = " << thr << ";" << endl;
		errfile1 << "thr = " << "0.10" << ";" << endl;
		errfile2 << "thr = " << "0.25" << ";" << endl;
		errfile3 << "thr = " << "0.50" << ";" << endl;
		errfile << "IIerrors = [ ";
		errfile1 <<"IIerrors = [ ";
		errfile2 << "IIerrors = [ ";
		errfile3 << "IIerrors = [ ";
		for (int i = 0; i < size; i++)
		{
			errfile << resultsII[i] << " ";
			errfile1 << thr10II[i] << " ";
			errfile2 << thr25II[i] << " ";
			errfile3 << thr50II[i] << " ";
		}
		errfile << "];" << endl;
		errfile1 <<"];"  << endl;
		errfile2 << "];" << endl;
		errfile3 << "];"  << endl;
		errfile << "FPIIerrors = [ ";
		errfile1 <<"FPIIerrors = [ ";
		errfile2 << "FPIIerrors = [ ";
		errfile3 << "FPIIerrors = [ ";
		for (int i = 0; i < size; i++)
		{
			errfile << resultsFPII[i] << " ";
			errfile1 << thr10FPII[i] << " ";
			errfile2 << thr25FPII[i] << " ";
			errfile3 << thr50FPII[i] << " ";
		}
		errfile << "];" << endl;
		errfile1 << "];" << endl;
		errfile2 << "];" << endl;
		errfile3 << "];" << endl;

		resultsII[id] = zernikeII.getFeaturesCount();
		resultsFPII[id] = zernikeFPII.getFeaturesCount();

		for (int i = 0; i < height; i++)
			delete[] grayImage[i];
		delete[] grayImage;

		errfile.close();
		errfile1.close();
		errfile2.close();
		errfile3.close();

		delete[] thr10II;
		delete[] thr10FPII;
		delete[] thr25II;
		delete[] thr25FPII;
		delete[] thr50II;
		delete[] thr50FPII;
	}
	catch(exception)
	{
		if (grayImage != nullptr)
		{
			for (int i = 0; i < height; i++)
				delete[] grayImage[i];
			delete[] grayImage;
		}

		delete[] thr10II;
		delete[] thr10FPII;
		delete[] thr25II;
		delete[] thr25FPII;
		delete[] thr50II;
		delete[] thr50FPII;

		errfile.close();
		errfile1.close();
		errfile2.close();
		errfile3.close();

		return ERRORS::UNKNOWN_ERROR;
	}
	return errorCode;
}

extern "C" __declspec(dllexport) int __stdcall Extraction(const char negativeSamplesPath[], const char positiveSamplesPath[], const char savePath[], const char extractor[], int* parameters, SaveFileType fileType, bool append, ProgressCallback progressCallback)
{
	int errorCode = 0;

	string negSamples = string(negativeSamplesPath);
	string posSamples = string(positiveSamplesPath);
	string save = string(savePath);
	string tmpSave = string(savePath);
	string extractorName = string(extractor);

	Extractor* ext = InitializeExtractor(extractorName, parameters, fileType);
	if (ext == nullptr)
		return ERRORS::UNKNOWN_EXTRACTOR;

	vector<string> vecP, vecN;
	vector<int> allClassesP, allClassesN;
	string line;

	ifstream file2(posSamples + "file.lst", ios::in);
	while (getline(file2, line))
	{
		vecP.push_back(line);
		allClassesP.push_back(1);
	}
	file2.close();

	ifstream file(negSamples + "file.lst", ios::in);
	while (getline(file, line))
	{
		vecN.push_back(line);
		allClassesN.push_back(-1);
	}
	file.close();

	bool fileExist = filesystem::exists(save);
	if (fileExist && append)
		tmpSave = save + " - tmp";

	if (vecP.size() > 0)
	{
		try
		{
			int n = (int)vecP.size();
			int i = 0;
			for (; i < n - PROGRESS_BUFFER; i += PROGRESS_BUFFER)
			{
				vector<string> paths;
				paths.reserve(PROGRESS_BUFFER);
				int* classes = new int[PROGRESS_BUFFER];
				for (int j = i; j < (i + PROGRESS_BUFFER); j++)
				{
					paths.push_back(vecP[j]);
					classes[j - i] = allClassesP[j];
				}
				auto [n1, n2, features] = ext->extractMultipleFeatures(paths);
				writeBinary(features, classes, n1, n2, tmpSave, append);
				clearData(features, classes, n1);
				append = true;
				progressCallback(i + PROGRESS_BUFFER);
			}

			vector<string> paths;
			paths.reserve(PROGRESS_BUFFER);
			int* classes = new int[PROGRESS_BUFFER];
			for (int j = i; j < n; j++)
			{
				paths.push_back(vecP[j]);
				classes[j - i] = allClassesP[j];
			}
			auto [n1, n2, features] = ext->extractMultipleFeatures(paths);
			writeBinary(features, classes, n1, n2, tmpSave, append);
			clearData(features, classes, n1);
			append = true;
			progressCallback(n);

			// jesli append
			// ekstrachowac pozytywy do osbonego pliku
			// dopisac negatywy
			// renamowac tmp plik
		}
		catch (ERRORS err)
		{
			errorCode = err;
		}
		catch (exception)
		{
			errorCode = ERRORS::UNKNOWN_ERROR;
		}
	}
	if (vecN.size() > 0)
	{
		try
		{
			int n = (int)vecN.size();
			int i = 0;
			for (; i < n - PROGRESS_BUFFER; i += PROGRESS_BUFFER)
			{
				vector<string> paths;
				paths.reserve(PROGRESS_BUFFER);
				int* classes = new int[PROGRESS_BUFFER];
				for (int j = i; j < (i + PROGRESS_BUFFER); j++)
				{
					paths.push_back(vecN[j]);
					classes[j - i] = allClassesN[j];
				}
				auto [n1, n2, features] = ext->extractMultipleFeatures(paths);
				writeBinary(features, classes, n1, n2, save, append);
				clearData(features, classes, n1);
				append = true;
				progressCallback(i + PROGRESS_BUFFER);
			}

			vector<string> paths;
			paths.reserve(PROGRESS_BUFFER);
			int* classes = new int[PROGRESS_BUFFER];
			for (int j = i; j < n; j++)
			{
				paths.push_back(vecN[j]);
				classes[j - i] = allClassesN[j];
			}
			auto [n1, n2, features] = ext->extractMultipleFeatures(paths);
			writeBinary(features, classes, n1, n2, save, append);
			clearData(features, classes, n1);
			append = true;
			progressCallback(n);
		}
		catch (ERRORS err)
		{
			errorCode = err;
		}
		catch (exception)
		{
			errorCode = ERRORS::UNKNOWN_ERROR;
		}
	}

	if (vecP.size() > 0 && tmpSave == save + " - tmp")
	{
		int total = 0;

		fstream myFileOld(save, ios_base::out | ios_base::in | ios_base::ate | ios_base::binary);
		myFileOld.seekg(0);
		int n1, n2;
		myFileOld.read(reinterpret_cast<char*>(&n1), sizeof(n1));
		myFileOld.read(reinterpret_cast<char*>(&n2), sizeof(n2));

		fstream myFileNew(tmpSave, ios_base::out | ios_base::in | ios_base::ate | ios_base::binary);
		myFileNew.seekg(0);
		int n3, n4;
		myFileNew.read(reinterpret_cast<char*>(&n3), sizeof(n3));
		myFileNew.read(reinterpret_cast<char*>(&n4), sizeof(n4));

		total = n1 + n3;

		myFileNew.seekp(0, ios::end);
		for (int i = 0; i < n1; i++)
		{
			double* X = new double[n2];
			int D;
			myFileOld.read(reinterpret_cast<char*>(X), sizeof(double)* n2);
			myFileOld.read(reinterpret_cast<char*>(&D), sizeof(int));
			myFileNew.write(reinterpret_cast<const char*>(X), sizeof(double)* n2);
			myFileNew.write(reinterpret_cast<const char*>(&D), sizeof(int));
			delete[] X;
		}
		myFileNew.seekp(0);
		myFileNew.write(reinterpret_cast<char*>(&total), sizeof(total));

		myFileOld.close();
		myFileNew.close();

		filesystem::remove(save);
		filesystem::rename(tmpSave, save);
	}

	if (vecP.size() == 0 && vecN.size() == 0)
		errorCode = ERRORS::CORRUPTED_FILE;

	if (ext != nullptr)
		delete ext;

	return errorCode;
}

extern "C" __declspec(dllexport) int __stdcall ExtractFromImage(const char extractor[], int* parameters, const unsigned char* image, const int bytesPerPixel,
	const int width, const int height, const int stride, Rectangle * rec, int winCount, Point * sizes, int sizesCount,
	const char savePath[], int cls)
{
	std::ofstream outfile;
	outfile.open("log.txt", std::ios_base::app);

	int errorCode = 0;

	string save = string(savePath);
	string extractorName = string(extractor);

	Extractor* ext = InitializeExtractor(extractorName, parameters);
	if (ext == nullptr)
		return ERRORS::UNKNOWN_EXTRACTOR;
	outfile << "utworzono ekstraktor" << endl;

	ext->initializeExtractor(sizes, sizesCount);
	outfile << "zakonczono inicjalizacje rozmiarow okien" << endl;
	for (int i = 0; i < sizesCount; i++)
		outfile << sizes[i].wx << "x" << sizes[i].wy << endl;

	// Przetworzenie obrazu
	const double* const* grayImage = ConvertToGray(image, bytesPerPixel, width, height, stride);
	ext->loadImageData(grayImage, height, width);
	int nx = ext->getWidth(), ny = ext->getHeight();
	outfile << "prztworzono do szarosc" << endl;
	outfile << nx << "x" << ny << endl;

	for (int i = 0; i < height; i++)
		delete[] grayImage[i];
	delete[] grayImage;

	const double** newFeatures = new const double* [winCount];
	int* classes = new int[winCount];
	int fc = 0;

	outfile << "przetworzenie okien" << endl;
#pragma omp parallel for num_threads(OMP_NUM_THR)
	for (int w = 0; w < winCount; w++)
	{
		auto [featureCount, feature] = ext->extractFromWindow(rec[w].w, rec[w].h, rec[w].x, rec[w].y);
		newFeatures[w] = feature;
		classes[w] = cls;
		fc = featureCount;
#pragma omp critical
		outfile << "okno " << w << ": " << rec[w].w << "x" << rec[w].h << "x: " << rec[w].x << "y: " << rec[w].y << endl;
	}
	writeBinary(newFeatures, classes, winCount, fc, save, true);
	outfile << "zapisano do pliku: " << savePath << endl;

	for (int i = 0; i < winCount; i++)
		delete[] newFeatures[i];
	delete[] newFeatures;
	delete[] classes;
	delete ext;

	outfile.close();

	return errorCode;
}

extern "C" __declspec(dllexport) int ExtractFromImageFinalize(const char path1[], const char path2[], bool firstOrignial)
{
	string oldPath(path1);
	string newPath(path2);

	int total = 0;

	fstream myFileOld(oldPath, ios_base::out | ios_base::in | ios_base::ate | ios_base::binary);
	myFileOld.seekg(0);
	int n1, n2;
	myFileOld.read(reinterpret_cast<char*>(&n1), sizeof(n1));
	myFileOld.read(reinterpret_cast<char*>(&n2), sizeof(n2));

	fstream myFileNew(newPath, ios_base::out | ios_base::in | ios_base::ate | ios_base::binary);
	myFileNew.seekg(0);
	int n3, n4;
	myFileNew.read(reinterpret_cast<char*>(&n3), sizeof(n3));
	myFileNew.read(reinterpret_cast<char*>(&n4), sizeof(n4));

	total = n1 + n3;
	if (n2 != n4)
	{
		myFileOld.close();
		myFileNew.close();
		return ERRORS::INCONSISTENT_FEATURES;
	}

	if (firstOrignial)
	{
		myFileOld.seekp(0, ios::end);
		for (int i = 0; i < n3; i++)
		{
			double* X = new double[n4];
			int D;
			myFileNew.read(reinterpret_cast<char*>(X), sizeof(double)* n4);
			myFileNew.read(reinterpret_cast<char*>(&D), sizeof(int));
			myFileOld.write(reinterpret_cast<const char*>(X), sizeof(double)* n4);
			myFileOld.write(reinterpret_cast<const char*>(&D), sizeof(int));
			delete[] X;
		}
		myFileOld.seekp(0);
		myFileOld.write(reinterpret_cast<char*>(&total), sizeof(total));

	}
	else
	{
		myFileNew.seekp(0, ios::end);
		for (int i = 0; i < n1; i++)
		{
			double* X = new double[n2];
			int D;
			myFileOld.read(reinterpret_cast<char*>(X), sizeof(double)* n2);
			myFileOld.read(reinterpret_cast<char*>(&D), sizeof(int));
			myFileNew.write(reinterpret_cast<const char*>(X), sizeof(double)* n2);
			myFileNew.write(reinterpret_cast<const char*>(&D), sizeof(int));
			delete[] X;
		}
		myFileNew.seekp(0);
		myFileNew.write(reinterpret_cast<char*>(&total), sizeof(total));
	}
	myFileOld.close();
	myFileNew.close();

	return 0;
}

extern "C" __declspec(dllexport) int __stdcall LoadLearningData(const char featuresPath[])
{
	string features = string(featuresPath);

	try
	{
		tie(allFeatures, allClasses, samplesCount, featuresCount) = readBinary(features);
	}
	catch (ERRORS err)
	{
		return err;
	}
	catch (exception)
	{
		return ERRORS::CORRUPTED_FEATURES_FILE;
	}

	return 0;
}

extern "C" __declspec(dllexport) void __stdcall FreeLearningData()
{
	clearData(allFeatures, allClasses, samplesCount);
}

extern "C" __declspec(dllexport) double __stdcall Learn(const char classifierPath[], const char classifierType[], ClassifierParameters parameters, const char validationSetPath[])
{
	double accurancy = 0.0;

	string classifierSavePath = string(classifierPath);
	string classifier = string(classifierType);
	string validationPath = string(validationSetPath);

	Classifier* cls = nullptr;
	try
	{
		cls = InitializeClassifier(classifier, parameters);
		if (cls == nullptr)
			return ERRORS::UNKNOWN_CLASSIFIER;

		if (cls->isCascade() && validationPath != "")
		{
			auto [xv, dv, n3, n4] = readBinary(validationPath);
			((Cascade*)cls)->train(allFeatures, allClasses, xv, dv, samplesCount, n3, featuresCount);
			clearData(xv, dv, n3);
		}
		else
			cls->train(allFeatures, allClasses, samplesCount, featuresCount);

		filesystem::path clsCurrentPath(classifierSavePath);
		if (!filesystem::exists(clsCurrentPath.parent_path()))
			classifierSavePath = clsCurrentPath.filename().string() + ".asv";
		cls->saveModel(classifierSavePath);

		if (validationPath == "")
		{
			int* out = cls->classify(allFeatures, samplesCount, featuresCount);
			accurancy = 1.0 - calculateError(allClasses, out, samplesCount);
			delete[] out;
		}
		else
		{
			try
			{
				auto [acc, far, sens] = cls->validate(validationPath);
				accurancy = acc;
			}
			catch (exception)
			{
				int* out = cls->classify(allFeatures, samplesCount, featuresCount);
				accurancy = 1.0 - calculateError(allClasses, out, samplesCount);
				delete[] out;
			}
		}
	}
	catch (ERRORS err)
	{
		accurancy = err;
	}
	catch (exception)
	{
		accurancy = ERRORS::UNKNOWN_ERROR;
	}

	if (cls != nullptr)
		delete cls;

	return accurancy;
}

extern "C" __declspec(dllexport) int __stdcall Testing(const char featuresPath[], const char classifierPath[], int* classes, double* thresholds, double* avgFeatures, ProgressCallback progressCallback)
{
	int errorCode = 0;

	string features = string(featuresPath);
	string classifier = string(classifierPath);
	string clsName;

	Classifier* cls = nullptr;

	double* out = nullptr;
	int* allClasses = nullptr;
	int samplesCount = 0;
	*avgFeatures = 0;

	try
	{
		ifstream file(classifier, ios::in);
		getline(file, clsName);
		getline(file, clsName);
		getline(file, clsName);
		getline(file, clsName);
		file.close();
		clsName = clsName.substr(clsName.find(" ") + 1);

		cls = InitializeClassifier(clsName, classifier);
		if (cls == nullptr)
			return ERRORS::UNKNOWN_CLASSIFIER;

		ifstream myFile(features, ios_base::binary);
		int featuresCount;
		readBinaryPartial(samplesCount, featuresCount, myFile);
		out = new double[samplesCount];
		allClasses = new int[samplesCount];

		progressCallback(samplesCount);
		progressCallback(0);

		int i = 0;
		for (; i < samplesCount - PROGRESS_BUFFER; i += PROGRESS_BUFFER)
		{
			int* D = nullptr;
			double** X = nullptr;
			try
			{
				tie(X, D) = readBinaryPartial(myFile, i, i + PROGRESS_BUFFER, featuresCount);
			}
			catch (exception)
			{
				if (cls != nullptr)
					delete cls;
				if (out != nullptr)
					delete[] out;
				if (allClasses != nullptr)
					delete[] allClasses;
				if (D != nullptr)
					delete[] D;
				if (X != nullptr)
				{
					for (int i = 0; i < PROGRESS_BUFFER; i++)
						delete[] X[i];
					delete[] X;
				}
				myFile.close();
				return ERRORS::CORRUPTED_FEATURES_FILE;
			}
			auto [tmp, afeat] = cls->calculateOutputN(X, PROGRESS_BUFFER, featuresCount);
			memcpy(out + i, tmp, sizeof(double) * PROGRESS_BUFFER);
			memcpy(allClasses + i, D, sizeof(int) * PROGRESS_BUFFER);
			delete[] tmp;
			*avgFeatures += afeat * PROGRESS_BUFFER;
			clearData(X, D, PROGRESS_BUFFER);

			progressCallback(i);
		}

		if (i < samplesCount)
		{
			int* D = nullptr;
			double** X = nullptr;
			try
			{
				tie(X, D) = readBinaryPartial(myFile, i, samplesCount, featuresCount);
			}
			catch (exception)
			{
				if (cls != nullptr)
					delete cls;
				if (out != nullptr)
					delete[] out;
				if (allClasses != nullptr)
					delete[] allClasses;
				if (D != nullptr)
					delete[] D;
				if (X != nullptr)
				{
					for (int i = 0; i < PROGRESS_BUFFER; i++)
						delete[] X[i];
					delete[] X;
				}
				myFile.close();
				return ERRORS::CORRUPTED_FEATURES_FILE;
			}
			int offset = samplesCount - i;
			auto [tmp, afeat] = cls->calculateOutputN(X, offset, featuresCount);
			memcpy(out + i, tmp, sizeof(double) * offset);
			memcpy(allClasses + i, D, sizeof(int) * offset);
			delete[] tmp;
			*avgFeatures += afeat * offset;
			clearData(X, D, offset);
		}
		*avgFeatures = *avgFeatures / samplesCount;
		myFile.close();

		progressCallback(samplesCount);
	}
	catch (ERRORS err)
	{
		errorCode = err;
	}
	catch (exception)
	{
		errorCode = ERRORS::UNKNOWN_ERROR;
	}

	if (errorCode == 0)
	{
		memcpy(classes, allClasses, sizeof(int) * samplesCount);
		memcpy(thresholds, out, sizeof(double) * samplesCount);
	}

	if (cls != nullptr)
		delete cls;
	if (out != nullptr)
		delete[] out;
	if (allClasses != nullptr)
		delete[] allClasses;

	return errorCode;
}

Extractor* extractor = nullptr;
Classifier* classifier = nullptr;
extern "C" __declspec(dllexport) int __stdcall LoadExtractor(const char extractorName[], int* extractorParameters)
{
	string extName = string(extractorName);

	if (extractor != nullptr)
	{
		delete extractor;
		extractor = nullptr;
	}

	// Przygotowanie ekstraktora
	extractor = InitializeExtractor(extractorName, extractorParameters);
	if (extractor == nullptr)
		return ERRORS::UNKNOWN_EXTRACTOR;

	return 0;
}

extern "C" __declspec(dllexport) int __stdcall LoadClassifier(const char classifierPath[])
{
	string clsPath = string(classifierPath);
	string clsName;

	if (classifier != nullptr)
	{
		delete classifier;
		classifier = nullptr;
	}

	ifstream file(clsPath, ios::in);
	getline(file, clsName);
	getline(file, clsName);
	getline(file, clsName);
	getline(file, clsName);
	file.close();
	clsName = clsName.substr(clsName.find(" ") + 1);

	classifier = InitializeClassifier(clsName, clsPath);
	if (classifier == nullptr)
		return ERRORS::UNKNOWN_CLASSIFIER;

	return 0;
}

extern "C" __declspec(dllexport) int __stdcall TestDetectionTime(const int repetitions, const DetectionParameters detectionParameters, long long* results,
	const unsigned char* image, const int bytesPerPixel, const int width, const int height, const int stride, Rectangle * rec, int winCount, Point * sizes, ProgressCallback progressCallback)
{
	int errorCode = 0;

	try
	{
		// Przetworzenie obrazu
		const double* const* &&grayImage = ConvertToGray(image, bytesPerPixel, width, height, stride);

		extractor->loadImageData(grayImage, height, width);

		for (int i = 0; i < height; i++)
			delete[] grayImage[i];
		delete[] grayImage;


		//string imagePath = "E:\\Datasets\\Letters\\det_test\\detection191.gray.8bin";
		//extractor->loadImageData(imagePath);

		extractor->initializeExtractor(sizes, detectionParameters.scales);

		for (int ind = 0; ind < repetitions; ind++)
		{
			std::chrono::system_clock::time_point begind = std::chrono::system_clock::now();

#pragma omp parallel num_threads(OMP_NUM_THR)
			{
				double* features = new double[extractor->getFeaturesCount()];
#pragma omp for 
				for (int w = 0; w < winCount; w++)
					classifier->calculateOutputForWindow(extractor, rec[w].w, rec[w].h, rec[w].x, rec[w].y, features);
				delete[] features;
			}

			std::chrono::system_clock::time_point endd = std::chrono::system_clock::now();
			results[ind] = std::chrono::duration_cast<std::chrono::nanoseconds> (endd - begind).count();
			results[repetitions] = winCount;

			progressCallback(ind + 1);
		}

		int fCount;
		double suma = 0;
//#pragma omp parallel num_threads(OMP_NUM_THR)
		{
			double* features = new double[extractor->getFeaturesCount()];
//#pragma omp for reduction(+:suma)
			for (int w = 0; w < winCount; w++)
			{
				tie(ignore, fCount) = classifier->calculateOutputForWindowN(extractor, rec[w].w, rec[w].h, rec[w].x, rec[w].y, features);
				suma += fCount;
			}
			delete[] features;
		}
		results[repetitions + 1] = round((suma / winCount) * 1000);

		extractor->clearImageData();
	}
	catch (ERRORS err)
	{
		errorCode = err;
	}
	catch (exception)
	{
		errorCode = ERRORS::UNKNOWN_ERROR;
	}

	return errorCode;
}


extern "C" __declspec(dllexport) int __stdcall Detection(const DetectionParameters detectionParameters, double* outputs,
	const unsigned char* image, const int bytesPerPixel, const int width, const int height, const int stride, Rectangle * rec, int winCount, Point * sizes)
{
	int errorCode = 0;

	try
	{
		extractor->initializeExtractor(sizes, detectionParameters.scales);

		// Przetworzenie obrazu
		const double* const* grayImage = ConvertToGray(image, bytesPerPixel, width, height, stride);
		extractor->loadImageData(grayImage, height, width);
		//int nx = extractor->getWidth(), ny = extractor->getHeight();

		for (int i = 0; i < height; i++)
			delete[] grayImage[i];
		delete[] grayImage;


		//string imagePath = "E:\\Datasets\\Letters\\det_test\\detection191.gray.8bin";
		//extractor->loadImageData(imagePath);

#pragma omp parallel num_threads(OMP_NUM_THR)
		{
			double* features = new double[extractor->getFeaturesCount()];
#pragma omp for 
			for (int w = 0; w < winCount; w++)
				outputs[w] = classifier->calculateOutputForWindow(extractor, rec[w].w, rec[w].h, rec[w].x, rec[w].y, features);
			delete[] features;
		}

		extractor->clearImageData();
	}
	catch (ERRORS err)
	{
		errorCode = err;
	}
	catch (exception)
	{
		errorCode = ERRORS::UNKNOWN_ERROR;
	}

	return errorCode;
}

extern "C" __declspec(dllexport) int __stdcall RemoveSamples(const char removePath[], bool negative, bool positive)
{
	string path(removePath);

	ifstream myFile(path, ios_base::binary);

	// wczytanie rozmiaru macierzy
	int n1, n2;
	myFile.read(reinterpret_cast<char*>(&n1), sizeof(n1));
	myFile.read(reinterpret_cast<char*>(&n2), sizeof(n2));

	// wczytanie kolejnych probek oraz ich klas
	double** X = new double* [n1 + 1];
	int* D = new int[n1];

	int i2 = 0;
	X[i2] = new double[n2];
	for (int i = 0; i < n1; i++)
	{
		myFile.read(reinterpret_cast<char*>(X[i2]), sizeof(double)* n2);
		myFile.read(reinterpret_cast<char*>(&D[i2]), sizeof(int));
		if (D[i2] == -1 && negative)
			continue;
		if (D[i2] == 1 && positive)
			continue;
		i2++;
		X[i2] = new double[n2];
	}
	myFile.close();
	writeBinary(X, D, i2, n2, path, false);

	delete[] D;
	for (int i = 0; i < i2 + 1; i++)
		delete[] X[i];
	delete[] X;

	return 0;
}

// logowanie czasów:
//ofstream myfile;
//myfile.open("timelogs.txt");
//std::chrono::system_clock::time_point beging = std::chrono::system_clock::now();
//std::chrono::system_clock::time_point endg = std::chrono::system_clock::now();
//myfile << "Total time" << endl;
//myfile << "Time difference = " << std::chrono::duration_cast<std::chrono::seconds>(endg - beging).count() << "s" << endl;
//myfile << "Time difference = " << std::chrono::duration_cast<std::chrono::nanoseconds> (endg - beging).count() << "ns" << endl;
//myfile.close();