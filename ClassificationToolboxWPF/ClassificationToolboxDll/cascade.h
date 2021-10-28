#pragma once
#include<iostream>
#include<unordered_set>
#include<queue>
#include<set>
#include<omp.h>
#include<filesystem>

#include"utills.h"
#include"configuration.h"
#include"feature.h"
#include"classifier.h"
#include"boosting.h"

int NEGATIVE_VALUE = -10000;

using namespace std;

/// <summary>Klasa bazowa dla klasyfikatorow boostingowych</summary>
class Cascade : public Classifier
{
protected:
	int stagesCount = 0;
	int K; /// <summary>Liczba etapow</summary>
	double D; /// <summary>Minmialna czulosc</summary>
	double A; /// <summary>Maksymalny FAR</summary>
	string learningMethod = "VJ";

	bool ForceSeeds = true;
	long ValidSeed1 = 12801;
	long ValidSeed2 = 1021;
	long TrainSeed1 = 151;
	long TrainSeed2 = 3457;
	bool ResizeSetsWhenResampling = true;
	int ResamplingMaxValSize[1] = { 25000 };
	int ResamplingMaxTrainSize[2] = { 50000, 35000 };

	int** featurePerStage = nullptr;
	int* featuresCounts = nullptr;

	double Ev = 0.0;
	double Ex = 0.0;

public:
	using Classifier::train;
	using Classifier::getFeatures;

	virtual ~Cascade()
	{
		if (featurePerStage != nullptr)
		{
			for (int i = 0; i < stagesCount; i++)
				delete[] featurePerStage[i];
			delete[] featurePerStage;
			delete[] featuresCounts;
		}
	}

	virtual bool isCascade() { return true; }

	virtual int getStagesNumber()
	{
		return stagesCount;
	}

	virtual const tuple<const int, const int*> getFeatures(int stage)
	{
		if (stage < stagesCount)
			return make_tuple(featuresCounts[stage], featurePerStage[stage]);
		else
			throw ERRORS::UNKNOWN_ERROR;
	}

	virtual void train(const double* const* Xtrain, const int* Dtrain, const double* const* Xvalidate, const int* Dvalidate, int trainigSamples, int validationSamples, int attribiutesCount) = 0;

	void train(const double* const* X, const int* D, const int* indices, int samplesCount, int attributesCount, int indicesCount) override
	{
		auto[Xtrain, Dtrain, Xvalidate, Dvalidate, trainigSamples, validationSamples] = split(X, D, samplesCount, attributesCount, 0.7);
		train(Xtrain, Dtrain, Xvalidate, Dvalidate, trainigSamples, validationSamples, attributesCount);

		delete[] Xtrain;
		delete[] Xvalidate;
		delete[] Dtrain;
		delete[] Dvalidate;
	}

	virtual void train(const double* const* Xtrain, const int* Dtrain, int trainigSamples, int attribiutesCount, string validationPath)
	{
		try
		{
			auto[Xvalidate, Dvalidate, validationSamples, n2] = readBinary(validationPath);
			train(Xtrain, Dtrain, Xvalidate, Dvalidate, trainigSamples, validationSamples, attribiutesCount);
			clearData(Xvalidate, Dvalidate, validationSamples);
		}
		catch (exception)
		{
			throw ERRORS::CORRUPTED_FEATURES_FILE;
		}
	}
};

/// <summary>Kaskada klasyfiaktorow --- wersja klasyczna</summary>
class CascadeOfClassifier : public Cascade
{
private:
	ClassifierParameters parameters;  /// <summary>Struktura z parametrami dla klasyfiaktorow</summary>
	int maxBoostingStages = 100;

	double* thresholds = nullptr; /// <summary>Progi dla klasyfikatorow</summary>
	Boosting** stages = nullptr; /// <summary>Wybrane w wyniku nauki klasyfiaktory (boostingowe)</summary>
	string boostingType = RealBoost::GetType(); /// <summary>Typ algorytmu boostingowego</summary>

	double* ais = nullptr;
	double* dis = nullptr;

public:
	using Classifier::train;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::saveModel;
	using Classifier::saveModelOld;
	using Classifier::loadModel;

	~CascadeOfClassifier()
	{
		for (int i = 0; i < stagesCount; i++)
			delete stages[i];
		delete[] stages;
		delete[] thresholds;
		delete[] ais;
		delete[] dis;
	}

	/// <summary>Utworzenie  kaskady klasyfikatorow na podstawie domyslnych parametrow</summary>
	CascadeOfClassifier()
	{
		this->boostingType = parameters.boostingType;
		this->maxBoostingStages = parameters.boostingStages;
		this->K = parameters.cascadeStages;
		this->A = parameters.maxFAR;
		this->D = parameters.minSpecificity;
		learningMethod = "VJ";

		ais = new double[K];
		dis = new double[K];
		thresholds = new double[K];
		stages = new Boosting *[K];
		featurePerStage = new int*[K];
		featuresCounts = new int[K];

		ForceSeeds = parameters.forceSeeds;
		ValidSeed1 = parameters.validSeed1;
		ValidSeed2 = parameters.validSeed2;
		TrainSeed1 = parameters.trainSeed1;
		TrainSeed2 = parameters.trainSeed2;

		ResizeSetsWhenResampling = parameters.resizeSetsWhenResampling;
		ResamplingMaxValSize[0] = parameters.resamplingMaxValSize;
		ResamplingMaxTrainSize[0] = parameters.resamplingMaxTrainSize1;
		ResamplingMaxTrainSize[1] = parameters.resamplingMaxTrainSize2;
	}

	/// <summary>Utworzenie kaskady klasyfikatorow na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	CascadeOfClassifier(const ClassifierParameters& parameters)
	{
		this->parameters = parameters;

		this->boostingType = this->parameters.boostingType;
		this->maxBoostingStages = this->parameters.boostingStages;
		this->K = this->parameters.cascadeStages;
		this->A = this->parameters.maxFAR;
		this->D = this->parameters.minSpecificity;
		this->learningMethod = this->parameters.learningMethod;

		ais = new double[K];
		dis = new double[K];
		thresholds = new double[K];
		stages = new Boosting *[K];
		featurePerStage = new int*[K];
		featuresCounts = new int[K];

		ForceSeeds = this->parameters.forceSeeds;
		ValidSeed1 = this->parameters.validSeed1;
		ValidSeed2 = this->parameters.validSeed2;
		TrainSeed1 = this->parameters.trainSeed1;
		TrainSeed2 = this->parameters.trainSeed2;

		ResizeSetsWhenResampling = this->parameters.resizeSetsWhenResampling;
		ResamplingMaxValSize[0] = this->parameters.resamplingMaxValSize;
		ResamplingMaxTrainSize[0] = this->parameters.resamplingMaxTrainSize1;
		ResamplingMaxTrainSize[1] = this->parameters.resamplingMaxTrainSize2;
	}

	/// <summary>Zaladowanie kaskady klasyfikatorow z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	CascadeOfClassifier(string path) { loadModel(path); }

	/// <summary>Zaladowanie kaskady klasyfikatorow z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	CascadeOfClassifier(ifstream& input) { loadModel(input); }

	/// <summary>Zaladowanie kaskady klasyfikatorow z podanego strumienia oraz zapisanie parametrow w odpowiedniej strukturze</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'params'>Struktura z parametrami dla klasyfiaktora</param>
	CascadeOfClassifier(ifstream& input, ClassifierParameters& params) { loadModel(input, params); }

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "ClassifierCascade";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca instacje boostingowego klasyfikatora</summary>
	/// <returns>Instacja boostingowego klasyfikatora</returns>
	Boosting* InitalizeBoostedClassifier()
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost(parameters);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost(parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	/// <summary>Zwraca instacje boostingowego klasyfikatora utworzonego na podstawie danych z pliku</summary>
	/// <param name = 'input'>Strumien do pliku, w ktortm zostal zapisany slaby klasyfikator</param>
	/// <returns>Instacja boostingowego klasyfikatora</returns>
	Boosting* InitalizeBoostedClassifier(ifstream& input)
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost(input, parameters);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost(input, parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <param name = 'full'>Pe?ny/Skrócony opis klasyfikatora</param>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";

		text += "Used boosting type: " + boostingType + "\r\n";
		text += "Learning method: " + learningMethod + "\r\n";
		text += "Stages count: " + to_string(stagesCount) + "\r\n";
		text += "Max FAR: " + to_string(A) + "\r\n";
		text += "Min sensitivity: " + to_string(D) + "\r\n";

		for (int i = 0; i < stagesCount; i++)
		{
			text += "Stage " + to_string(i) + ":\r\n";
			text += "Threshold " + to_string(thresholds[i]) + ":\r\n";
			text += "Boosting stages: " + to_string(stages[i]->getStagesNumber()) + "\r\n";
		}

		text += "\r\n";
		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		if (thresholds != nullptr)
		{
			for (int i = 0; i < stagesCount; i++)
				delete stages[i];
			delete[] stages;
			delete[] thresholds;
			for (int i = 0; i < stagesCount; i++)
				delete[] featurePerStage[i];
			delete[] featurePerStage;
			delete[] featuresCounts;
			delete[] features;
			delete[] ais;
			delete[] dis;
		}

		string fieldName, type;
		skipHeader(input);
		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> stagesCount;
			input >> fieldName >> boostingType;
			input >> fieldName >> maxBoostingStages;
			input >> fieldName >> Ev;
			input >> fieldName >> Ex;
			input >> fieldName >> A;
			input >> fieldName >> D;
			input >> fieldName >> K;
			input >> fieldName >> featuresCount;

			features = new int[featuresCount];
			featuresCounts = new int[K];
			featurePerStage = new int*[K];
			ais = new double[K];
			dis = new double[K];

			skipHeader(input);
			int k = 0;
			for (int i = 0; i < stagesCount; i++)
			{
				input >> fieldName >> featuresCounts[i];
				featurePerStage[i] = new int[featuresCounts[i]];
				for (int j = 0; j < featuresCounts[i]; j++)
				{
					input >> featurePerStage[i][j];
					features[k] = featurePerStage[i][j];
					k++;
				}
			}
			for (int i = stagesCount; i < K; i++)
				featurePerStage[i] = nullptr;

			thresholds = new double[K];
			stages = new Boosting *[K];
			skipHeader(input);
			for (int i = 0; i < stagesCount; i++)
			{
				skipHeader(input);
				input >> fieldName >> thresholds[i];
				stages[i] = InitalizeBoostedClassifier(input);
			}
			for (int i = stagesCount; i < K; i++)
				stages[i] = nullptr;

			skipHeader(input);
			input >> fieldName >> learningMethod;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;

		parameters.cascadeStages = K;
		parameters.maxFAR = A;
		parameters.minSpecificity = D;
		parameters.boostingStages = maxBoostingStages;
		strncpy_s(parameters.boostingType, ClassifierParameters::STRING_BUFFER, boostingType.c_str(), _TRUNCATE);
		strncpy_s(parameters.learningMethod, ClassifierParameters::STRING_BUFFER, learningMethod.c_str(), _TRUNCATE);
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'param'>Struktura z parametrami dla klasyfikatora</param>
	void loadModel(ifstream& input, ClassifierParameters& params) override
	{
		loadModel(input);

		// TODO Get weak classifier and boosting parameters

		params.cascadeStages = K;
		params.maxFAR = A;
		params.minSpecificity = D;
		params.boostingStages = maxBoostingStages;
		strncpy_s(params.boostingType, ClassifierParameters::STRING_BUFFER, boostingType.c_str(), _TRUNCATE);
		strncpy_s(params.learningMethod, ClassifierParameters::STRING_BUFFER, learningMethod.c_str(), _TRUNCATE);
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Stages: " << stagesCount << endl;
		output << "Boosting_Type: " << boostingType << endl;
		output << "Boosting_Stages: " << maxBoostingStages << endl;
		output << "Ex(s): " << Ev << endl;
		output << "Ex(f): " << Ex << endl;
		output << "A: " << A << endl;
		output << "D: " << D << endl;
		output << "K: " << K << endl;
		output << "Total_features: " << featuresCount << endl;
		createSecondaryHeader(output, "Features:");
		for (int i = 0; i < stagesCount; i++)
		{
			output << "Features_Count: " << featuresCounts[i] << endl;
			for (int j = 0; j < featuresCounts[i]; j++)
				output << featurePerStage[i][j] << " ";
			output << endl;
		}
		createSecondaryHeader(output, "Boosted_Classifiers:");
		for (int i = 0; i < stagesCount; i++)
		{
			createSecondaryHeader(output, "Boosted_Classifier_" + to_string(i));
			output << "Threshold: " << thresholds[i] << endl;
			stages[i]->saveModel(output);
		}
		createSecondaryHeader(output, "Additional_Info:");
		output << "Training_method: " << learningMethod << endl;
		output << "Use_graphs: " << false << endl;
		output << "Is_uniform: " << true << endl;
		createSecondaryHeader(output, "Resampling_Settings:");
		output << "Extractor: " << parameters.extractorType << endl;
		if (parameters.extractorType == HaarExtractor::GetType())
		{
			output << "Templates: " << parameters.t << endl;
			output << "Scales: " << parameters.s << endl;
			output << "Positions: " << parameters.ps << endl;
		}
		else if (parameters.extractorType == HOGExtractor::GetType())
		{
			output << "Bins: " << parameters.b << endl;
			output << "Blocks (X): " << parameters.nx << endl;
			output << "Blocks (Y): " << parameters.ny << endl;
		}
		else
		{
			output << "Harmonic: " << parameters.p << endl;
			output << "Degree: " << parameters.q << endl;
			output << "Rings: " << parameters.r << endl;
			output << "Rings_type: " << parameters.rt << endl;
			output << "Width: " << parameters.d << endl;
			output << "Overlap: " << parameters.w << endl;
		}
		output << "Resampling_scales: " << parameters.resScales << endl;
		output << "Scaling_factor: " << parameters.scaleFactor << endl;
		output << "Min_window_size: " << parameters.minWindow << endl;
		output << "Jumping_factor: " << parameters.jumpingFactor << endl;
		output << "Repetition_per_image: " << parameters.repetitionPerImage << endl;
		if (ais != nullptr)
		{
			createSecondaryHeader(output, "Validation_Scores:");
			output << "ai: ";
			for (int i = 0; i < stagesCount; i++)
				output << ais[i] << " ";
			output << endl;
			output << "di: ";
			for (int i = 0; i < stagesCount; i++)
				output << dis[i] << " ";
			output << endl;
		}
	}

	void saveModelOld(ofstream& output) const override
	{
		output << "ClassifierCascadeVJ_v2" << endl;
		output << Ev << endl;
		output << A << endl;
		output << 0 << endl;
		output << D << endl;
		output << 0 << endl;
		output << K << endl;
		output << stagesCount << endl;
		output << boostingType << endl;
		for (int i = 0; i < (int)stagesCount; i++)
		{
			output << thresholds[i] << endl;
			stages[i]->saveModelOld(output);
		}
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Cechy próbek ucz?cych</param>
	/// <param name = 'D'>Klasy próbek ucz?cych</param>
	/// <param name = 'Indices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	void train(const double* const* XtrainIn, const int* DtrainIn, const double* const* XvalidateIn, const int* DvalidateIn, int trainigSamples, int validationSamples, int attribiutesCount) override
	{
#pragma region Aloklacja pamieci
		if (stagesCount > 0)
		{
			for (int i = 0; i < stagesCount; i++)
				delete stages[i];
			for (int i = 0; i < stagesCount; i++)
				delete[] featurePerStage[i];
			delete[] features;
		}

		const double** Xtrain = nullptr;
		const double** Xvalidate = nullptr;
		int* Dtrain = nullptr;
		int* Dvalidate = nullptr;
		int* indices = nullptr;
		if (ResizeSetsWhenResampling)
		{
			int maxSamplesTrain = max(trainigSamples, ResamplingMaxTrainSize[0]);
			int maxSamplesValidate = max(validationSamples, ResamplingMaxValSize[0]);


			Xtrain = new const double*[maxSamplesTrain];
			Dtrain = new int[maxSamplesTrain];
			Xvalidate = new const double*[maxSamplesValidate];
			Dvalidate = new int[maxSamplesValidate];

			indices = new int[maxSamplesTrain];
			for (int i = 0; i < maxSamplesTrain; i++)
				indices[i] = i;
		}
		else
		{
			Xtrain = new const double*[trainigSamples];
			Xvalidate = new const double*[validationSamples];
			Dtrain = new int[trainigSamples];
			Dvalidate = new int[validationSamples];

			indices = new int[trainigSamples];
			for (int i = 0; i < trainigSamples; i++)
				indices[i] = i;
		}

		int originalTrainingSamples = trainigSamples;
		int originalValidationSamples = validationSamples;

		memcpy(Xtrain, XtrainIn, sizeof(double*) * trainigSamples);
		memcpy(Dtrain, DtrainIn, sizeof(int) * trainigSamples);
		memcpy(Xvalidate, XvalidateIn, sizeof(double*) * validationSamples);
		memcpy(Dvalidate, DvalidateIn, sizeof(int) * validationSamples);
#pragma endregion Aloklacja pamieci

		unordered_set<int> usedFeatures;

		int currentResamplingImage = -1;

		ofstream errfile;
		errfile.open("errorlogcascade" + learningMethod + "_" + to_string(A) + " " + to_string(D) + " " + to_string(K) + " " + parameters.boostingType + " " + parameters.extractorType + ".txt");
		errfile << "A: " << A << endl;
		errfile << "D: " << D << endl;

		// Przygotowanie ekstraktora cech do resamplingu
		string extractorType = string(parameters.extractorType);
		int extParams[6];
		if (extractorType == HaarExtractor::GetType())
		{
			extParams[0] = parameters.t;
			extParams[1] = parameters.s;
			extParams[2] = parameters.ps;
		}
		else if (extractorType == HOGExtractor::GetType())
		{
			extParams[0] = parameters.b;
			extParams[1] = parameters.nx;
			extParams[2] = parameters.ny;
		}
		else
		{
			extParams[0] = parameters.p;
			extParams[1] = parameters.q;
			extParams[2] = parameters.r;
			extParams[3] = parameters.rt;
			extParams[4] = parameters.d;
			extParams[5] = parameters.w;
		}
		Extractor* extractor = InitializeExtractor(extractorType, extParams, SaveFileType::binary8bit);

		// Przygotowanie listy plikow do resamplingu
		string nonFaceImagesFolder = string(parameters.nonFaceImagesPath);
		vector<string> imageList;
		for (auto& p : filesystem::directory_iterator(nonFaceImagesFolder))
		{
			string path = p.path().string();
			if (path.size() > 9 && path.substr(path.size() - 9) == "gray.8bin")
				imageList.push_back(path);
		}

		// Inicjalizacja prawdopobienstw dla skal oraz rozmiarow okien
		const int scales = parameters.resScales;
		const int minWindow = parameters.minWindow;
		const double scalingRatio = parameters.scaleFactor;
		const double jumpingRatio = parameters.jumpingFactor;
		const int repetitionsPerImage = parameters.repetitionPerImage;

		double* scalesProbability = new double[scales];
		double* windows = new double[scales];
		double windowSum = 1;
		windows[0] = 1;
		for (int s = 1; s < scales; s++)
		{
			windows[s] = windows[s - 1] * pow(1.0 / scalingRatio, 2);
			windowSum += windows[s];
		}
		for (int s = 0; s < scales; s++)
		{
			scalesProbability[s] = windows[s] / windowSum;
		}
		delete[] windows;

		// Wyznaczenie punktu rozdielajacego pozytwy od negatywow dla proby walidujacej
		int positiveEndIndexValidate = 0;
		for (; positiveEndIndexValidate < validationSamples; positiveEndIndexValidate++)
			if (Dvalidate[positiveEndIndexValidate] != 1)
				break;

		int positiveEndIndexTrain = 0;
		for (; positiveEndIndexTrain < trainigSamples; positiveEndIndexTrain++)
			if (Dtrain[positiveEndIndexTrain] != 1)
				break;


		int* outputOrder = new int[positiveEndIndexValidate];

		errfile << "val size: " << validationSamples << endl;
		errfile << "train size: " << trainigSamples << endl;

		double Aprev = 1.0, Ai, Di = 1.0;
		double aMax = pow(A, 1.0 / K);
		double dMin = pow(D, 1.0 / K);
		int fi;

		int it = 0;
		double probability = 1.0;
		bool isUGMG = learningMethod == "UGM-G";
		bool isUGM = learningMethod == "UGM";
		while (Aprev > A&& it < K)
		{
			it++;
			fi = 0;
			Ai = Aprev;

			double a = aMax;
			double d = dMin;
			if (isUGMG)
			{
				a = A / (Ai * pow(aMax, K - it));
				d = D / (Di * pow(dMin, K - it));
			}
			else if (isUGM)
			{
				a = pow(A / Ai, 1 / (K - it + 1.0));
				d = pow(D / Di, 1 / (K - it + 1.0));
			}

			errfile << "n: " << it << endl;
			errfile << "a_max: " << a << endl;
			errfile << "d_min: " << d << endl;

			// Przygotowanie boostingowego klasyfikatora na danym etapie
			Boosting* bClassifier = InitalizeBoostedClassifier();
			double clsThreshold;
			double* weights = nullptr;
			double* clsOutput = nullptr;

			int bStages = 0;
			double ai = INFINITY, di = INFINITY;
			while (ai > a&& bStages < maxBoostingStages)
			{
				delete[] clsOutput;

				// Dodanie slabego klasyfikatora
				bStages++;
				bClassifier->addStage(Xtrain, Dtrain, indices, weights, trainigSamples, attribiutesCount, trainigSamples);
				// Wyznaczenie wyjsc dla proby walidujacej
				clsOutput = bClassifier->calculateOutput(Xvalidate, validationSamples, attribiutesCount);

				// Posortowanie wyjsc dla probek pozytwynych w celu wyznaczenia czulosci

				for (int i = 0; i < positiveEndIndexValidate; i++)
					outputOrder[i] = i;
				sort_indexes(clsOutput, outputOrder, positiveEndIndexValidate);

				// Wybor progu zapewniajacego zadana czulosc
				int thrID = (int)floor((positiveEndIndexValidate) * (1 - d));
				clsThreshold = clsOutput[outputOrder[thrID]];

				// Klasyfikacja probek z zadanym progiem
				int TP = 0, TN = 0, FP = 0, FN = 0;
				for (int s = 0; s < validationSamples; s++)
				{
					int Y = clsOutput[s] >= clsThreshold ? 1 : -1;
					if (Dvalidate[s] == Y && Dvalidate[s] == 1)
						TP++;
					else if (Dvalidate[s] == Y && Dvalidate[s] == -1)
						TN++;
					else if (Dvalidate[s] != Y && Dvalidate[s] == 1 && Y == -1)
						FN++;
					else
						FP++;
				}
				ai = 1.0 * (FP) / (FP + TN);
				di = 1.0 * (TP) / (TP + FN);

				if (di < d)
				{
					errfile << "di: " << di << " < d:" << d << " --- retrying" << endl;

					if (thrID > 0)
					{
						thrID--;
						clsThreshold = clsOutput[outputOrder[thrID]];

						// Klasyfikacja probek z zadanym progiem
						int TP = 0, TN = 0, FP = 0, FN = 0;
						for (int s = 0; s < validationSamples; s++)
						{
							int Y = clsOutput[s] >= clsThreshold ? 1 : -1;
							if (Dvalidate[s] == Y && Dvalidate[s] == 1)
								TP++;
							else if (Dvalidate[s] == Y && Dvalidate[s] == -1)
								TN++;
							else if (Dvalidate[s] != Y && Dvalidate[s] == 1 && Y == -1)
								FN++;
							else
								FP++;
						}
						ai = 1.0 * (FP) / (FP + TN);
						di = 1.0 * (TP) / (TP + FN);
					}

					if (di < d)
					{
						errfile << "di: " << di << " < d:" << d << " --- ending" << endl;

						delete[] weights;
						delete[] outputOrder;
						delete[] clsOutput;
						delete[] indices;

						for (int i = originalTrainingSamples; i < trainigSamples; i++)
							delete[] Xtrain[i];
						delete[] Xtrain;
						delete[] Dtrain;

						for (int i = originalValidationSamples; i < validationSamples; i++)
							delete[] Xvalidate[i];
						delete[] Xvalidate;
						delete[] Dvalidate;

						if (extractor != nullptr)
							delete extractor;

						throw ERRORS::UNKNOWN_ERROR;
					}
				}
			}
			delete[] weights;
			bClassifier->endStagewiseTraining(indices, attribiutesCount, trainigSamples);

			auto[currentFeatCount, currentFeat] = bClassifier->getFeatures();
			errfile << "-------------------" << endl;
			errfile << "stages: " << bStages << endl;
			errfile << "features: " << currentFeatCount << endl;
			errfile << "di: " << di << endl;
			errfile << "ai: " << ai << endl;

			stagesCount = it;
			stages[it - 1] = bClassifier;
			thresholds[it - 1] = clsThreshold;
			ais[it - 1] = ai;
			dis[it - 1] = di;

			featurePerStage[it - 1] = new int[currentFeatCount];
			int f2 = 0;
			for (int f = 0; f < currentFeatCount; f++)
			{
				int feat = currentFeat[f];
				if (usedFeatures.count(feat) == 0)
				{
					usedFeatures.insert(feat);
					featurePerStage[it - 1][f2] = feat;
					f2++;
				}
			}
			featuresCounts[it - 1] = f2;

			if (it == 1)
			{
				Ev = currentFeatCount;
				Ex = f2;
			}
			else
			{
				Ev += currentFeatCount * probability;
				Ex += f2 * probability;
			}
			probability *= ai;

			Di *= di;
			Aprev *= ai;

			errfile << "A: " << Aprev << endl;
			errfile << "D: " << Di << endl;

#pragma region Resampling
			if (Aprev > A)
			{
				int* scalesNumbers = new int[repetitionsPerImage];
				int* xs = new int[repetitionsPerImage];
				int* ys = new int[repetitionsPerImage];
				int* wxs = new int[repetitionsPerImage];
				int* wys = new int[repetitionsPerImage];
				//const double** featuresList = new const double* [repetitionsPerImage];
				int fc = 0;

				errfile << "-------------------" << endl;
				errfile << "val size: " << validationSamples << endl;
				errfile << "train size: " << trainigSamples << endl;
				errfile << "-------------------" << endl;

				int sampleLimitVal = ResizeSetsWhenResampling ? ResamplingMaxValSize[0] : validationSamples;
				int valAdded = 0, t = 0;
				for (; t < positiveEndIndexValidate; t++)
				{
					if (clsOutput[t] >= clsThreshold)
					{
						Xvalidate[valAdded] = Xvalidate[t];
						Dvalidate[valAdded] = Dvalidate[t];
						valAdded++;
					}
				}
				positiveEndIndexValidate = valAdded;

				for (; t < originalValidationSamples && valAdded < sampleLimitVal; t++)
				{
					if (clsOutput[t] >= clsThreshold)
					{
						Xvalidate[valAdded] = Xvalidate[t];
						Dvalidate[valAdded] = Dvalidate[t];
						valAdded++;
					}
				}
				t = originalValidationSamples;
				originalValidationSamples = valAdded;

				for (; t < validationSamples && valAdded < sampleLimitVal; t++)
				{
					if (clsOutput[t] >= clsThreshold)
					{
						Xvalidate[valAdded] = Xvalidate[t];
						Dvalidate[valAdded] = Dvalidate[t];
						valAdded++;
					}
					else
						delete[] Xvalidate[t];
				}

				for (; t < validationSamples; t++)
					delete[] Xvalidate[t];
				validationSamples = sampleLimitVal;

				errfile << "val size (po usuniecu): " << valAdded << endl;
				errfile << "val positive (po usuniecu): " << positiveEndIndexValidate << endl;

				errfile << "rep per image: " << repetitionsPerImage << endl;
				for (int s = scales - 1; s >= 0; s--)
					errfile << "scale " << s << " prob: " << scalesProbability[s] << endl;

				if (ForceSeeds)
				{
					errfile << "seed val: " << ValidSeed1 + ValidSeed2 * it << endl;
					srand(ValidSeed1 + ValidSeed2 * it);
				}
				while (valAdded < validationSamples)
				{
					currentResamplingImage = rand() % (imageList.size());

					// ladowanie obrazu
					extractor->loadImageData(imageList[currentResamplingImage]);
					int nx = extractor->getWidth(), ny = extractor->getHeight();

					for (int rep = 0; rep < repetitionsPerImage; rep++)
					{
						// losownie skali
						double scaleTest = static_cast <double> (rand()) / static_cast <double> (RAND_MAX);
						int scaleNumber = 0;
						double threshold = 0;
						for (int s = scales - 1; s >= 0; s--)
						{
							if (scaleTest < scalesProbability[s] + threshold)
							{
								scaleNumber = s;
								break;
							}
							threshold += scalesProbability[s];
						}

						int wx = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
						int wy = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
						if (wx > nx) wx = nx;
						if (wy > ny) wy = ny;
						wx = wy = min(wx, wy);
						if (wx % 2 == 1)
							wx = wy = wx - 1;

						int x = rand() % (nx - wx);
						int y = rand() % (ny - wy);

						scalesNumbers[rep] = scaleNumber;
						xs[rep] = x;
						ys[rep] = y;
						wxs[rep] = wx;
						wys[rep] = wy;
					}

					// alkowawac feautre per watek
#pragma omp parallel num_threads(OMP_NUM_THR)
					{
						const double* features;
#pragma omp for ordered schedule(static, 1)
						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							tie(fc, features) = extractor->extractFromWindow(wxs[rep], wys[rep], xs[rep], ys[rep]);
							int cls = this->classify(features, fc);

#pragma omp ordered
							{
								int addID = -1;
#pragma omp critical
								{
									if (valAdded < validationSamples && cls == 1)
									{
										addID = valAdded;
										valAdded++;
									}
								}

								if (addID > -1)
								{
									Xvalidate[addID] = features;
									Dvalidate[addID] = -1;
								}
								else
								{
									delete[] features;
								}
							}
						}
					}

					extractor->clearImageData();
				}

				int trSamples = it == 1 ? ResamplingMaxTrainSize[0] : ResamplingMaxTrainSize[1];
				int sampleLimitTr = ResizeSetsWhenResampling ? trSamples : trainigSamples;

				int trainingAdded = positiveEndIndexTrain;
				t = positiveEndIndexTrain;
				for (; t < originalTrainingSamples && trainingAdded < sampleLimitTr; t++)
				{
					if (bClassifier->classify(Xtrain[t], attribiutesCount, clsThreshold) == 1)
					{
						Xtrain[trainingAdded] = Xtrain[t];
						Dtrain[trainingAdded] = Dtrain[t];
						trainingAdded++;
					}
				}
				t = originalTrainingSamples;
				originalTrainingSamples = trainingAdded;
				for (; t < trainigSamples && trainingAdded < sampleLimitTr; t++)
				{
					if (bClassifier->classify(Xtrain[t], attribiutesCount, clsThreshold) == 1)
					{
						Xtrain[trainingAdded] = Xtrain[t];
						Dtrain[trainingAdded] = Dtrain[t];
						trainingAdded++;
					}
					else
					{
						delete[] Xtrain[t];
					}
				}

				for (; t < trainigSamples; t++)
					delete[] Xtrain[t];
				trainigSamples = sampleLimitTr;

				errfile << "train size: (po usuniecu) " << trainingAdded << endl;
				if (ForceSeeds)
				{
					errfile << "seed train: " << TrainSeed1 + TrainSeed2 * it << endl;
					srand(TrainSeed1 + TrainSeed2 * it);
				}
				while (trainingAdded < trainigSamples)
				{
					currentResamplingImage = rand() % (imageList.size());

					// ladowanie obrazu
					extractor->loadImageData(imageList[currentResamplingImage]);
					int nx = extractor->getWidth(), ny = extractor->getHeight();

					for (int rep = 0; rep < repetitionsPerImage; rep++)
					{
						// losownie skali
						double scaleTest = static_cast <double> (rand()) / static_cast <double> (RAND_MAX);
						int scaleNumber = 0;
						double threshold = 0;
						for (int s = scales - 1; s >= 0; s--)
						{
							if (scaleTest < scalesProbability[s] + threshold)
							{
								scaleNumber = s;
								break;
							}
							threshold += scalesProbability[s];
						}

						int wx = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
						int wy = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
						if (wx > nx) wx = nx;
						if (wy > ny) wy = ny;
						wx = wy = min(wx, wy);
						if (wx % 2 == 1)
							wx = wy = wx - 1;

						int x = rand() % (nx - wx);
						int y = rand() % (ny - wy);

						scalesNumbers[rep] = scaleNumber;
						xs[rep] = x;
						ys[rep] = y;
						wxs[rep] = wx;
						wys[rep] = wy;
					}

#pragma omp parallel num_threads(OMP_NUM_THR)
					{
						const double* features;
#pragma omp for ordered schedule(static, 1)
						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							tie(fc, features) = extractor->extractFromWindow(wxs[rep], wys[rep], xs[rep], ys[rep]);
							int cls = this->classify(features, fc);
#pragma omp ordered
							{
								int addID = -1;
#pragma omp critical
								{
									if (trainingAdded < trainigSamples && cls == 1)
									{
										addID = trainingAdded;
										trainingAdded++;
									}
								}

								if (addID > -1)
								{
									Xtrain[addID] = features;
									Dtrain[addID] = -1;
								}
								else
								{
									delete[] features;
								}
							}
						}
					}

					extractor->clearImageData();
				}

				errfile << "-------------------" << endl;
				errfile << "val size (doprobkowane): " << validationSamples << endl;
				errfile << "train size: (doprobkowane)" << trainigSamples << endl;

				delete[] scalesNumbers;
				delete[] xs;
				delete[] ys;
				delete[] wxs;
				delete[] wys;
			}
#pragma endregion Resampling

			delete[] clsOutput;
		}
		errfile.close();

		featuresCount = 0;
		for (int s = 0; s < stagesCount; s++)
			featuresCount += featuresCounts[s];

		features = new int[featuresCount];
		int f2 = 0;
		for (int s = 0; s < stagesCount; s++)
		{
			for (int f = 0; f < featuresCounts[s]; f++)
			{
				features[f2] = featurePerStage[s][f];
				f2++;
			}
		}

		delete[] scalesProbability;
		delete[] outputOrder;
		delete[] indices;

		for (int i = originalTrainingSamples; i < trainigSamples; i++)
			delete[] Xtrain[i];
		delete[] Xtrain;
		delete[] Dtrain;

		for (int i = originalValidationSamples; i < validationSamples; i++)
			delete[] Xvalidate[i];
		delete[] Xvalidate;
		delete[] Dvalidate;

		if (extractor != nullptr)
			delete extractor;
	}

	tuple<double, int> calculateOutputForWindowN(Extractor* ext, int wx, int wy, int x, int y, double* features) const override
	{
		//double* features = new double[ext->getFeaturesCount()];

		double out = -1;
		int fet = 0;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			int fc = ext->extractFromWindow(features, featurePerStage[i], featuresCounts[i], wx, wy, x, y);
			out = stages[i]->classify(features, fc, thresholds[i]);
			fet += featuresCounts[i];

			if (out != 1)
			{
				//delete[] features;
				return make_tuple(NEGATIVE_VALUE, fet);
			}
		}
		int fc = ext->extractFromWindow(features, featurePerStage[stagesCount - 1], featuresCounts[stagesCount - 1], wx, wy, x, y);
		out = stages[stagesCount - 1]->calculateOutput(features, fc) - thresholds[stagesCount - 1];
		fet += featuresCounts[stagesCount - 1];

		//delete[] features;
		return make_tuple(out, fet);
	}


	inline double calculateOutputForWindow(Extractor* ext, int wx, int wy, int x, int y, double* features) const override
	{
		//double* features = new double[ext->getFeaturesCount()];

		double out = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			int fc = ext->extractFromWindow(features, featurePerStage[i], featuresCounts[i], wx, wy, x, y);
			out = stages[i]->classify(features, fc, thresholds[i]);

			if (out != 1)
			{
				//delete[] features;
				return NEGATIVE_VALUE;
			}
		}
		int fc = ext->extractFromWindow(features, featurePerStage[stagesCount - 1], featuresCounts[stagesCount - 1], wx, wy, x, y);
		out = stages[stagesCount - 1]->calculateOutput(features, fc) - thresholds[stagesCount - 1];

		//delete[] features;
		return out;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy próbki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attribiutesCount) const override
	{
		int yp = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			yp = stages[i]->classify(X, attribiutesCount, thresholds[i]);

			if (yp != 1)
				return NEGATIVE_VALUE;
		}
		return stages[stagesCount - 1]->calculateOutput(X, attribiutesCount) - thresholds[stagesCount - 1];
	}

	inline tuple<double, int> calculateOutputN(const double* X, int attributesCount) const override
	{
		int features = 0;

		int yp = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			yp = stages[i]->classify(X, attributesCount, thresholds[i]);
			features += featuresCounts[i];

			if (yp != 1)
				return make_tuple(NEGATIVE_VALUE, features);
		}
		features += featuresCounts[stagesCount - 1];
		return  make_tuple(stages[stagesCount - 1]->calculateOutput(X, attributesCount) - thresholds[stagesCount - 1], features);
	}
};

/// <summary>Kaskada klasyfiaktorow --- wersja z grafem</summary>
class GraphCascadeOfClassifier : public Cascade
{
private:
	ClassifierParameters parameters;  /// <summary>Struktura z parametrami dla klasyfiaktorow</summary>
	int maxBoostingStages = 100;

	double* thresholds = nullptr; /// <summary>Progi dla klasyfikatorow</summary>
	Boosting** stages = nullptr; /// <summary>Wybrane w wyniku nauki klasyfiaktory (boostingowe)</summary>
	string boostingType = RealBoost::GetType(); /// <summary>Typ algorytmu boostingowego</summary>

	double* ais = nullptr;
	double* dis = nullptr;

public:
	using Classifier::train;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::saveModel;
	using Classifier::saveModelOld;
	using Classifier::loadModel;

	~GraphCascadeOfClassifier()
	{
		for (int i = 0; i < stagesCount; i++)
			delete stages[i];
		delete[] stages;
		delete[] thresholds;
		delete[] ais;
		delete[] dis;
	}

	/// <summary>Utworzenie  kaskady klasyfikatorow na podstawie domyslnych parametrow</summary>
	GraphCascadeOfClassifier()
	{
		this->boostingType = parameters.boostingType;
		this->maxBoostingStages = parameters.boostingStages;
		this->K = parameters.cascadeStages;
		this->A = parameters.maxFAR;
		this->D = parameters.minSpecificity;
		learningMethod = "VJ";

		ais = new double[K];
		dis = new double[K];
		thresholds = new double[K];
		stages = new Boosting *[K];
		featurePerStage = new int*[K];
		featuresCounts = new int[K];

		ForceSeeds = parameters.forceSeeds;
		ValidSeed1 = parameters.validSeed1;
		ValidSeed2 = parameters.validSeed2;
		TrainSeed1 = parameters.trainSeed1;
		TrainSeed2 = parameters.trainSeed2;

		ResizeSetsWhenResampling = parameters.resizeSetsWhenResampling;
		ResamplingMaxValSize[0] = parameters.resamplingMaxValSize;
		ResamplingMaxTrainSize[0] = parameters.resamplingMaxTrainSize1;
		ResamplingMaxTrainSize[1] = parameters.resamplingMaxTrainSize2;
	}

	/// <summary>Utworzenie kaskady klasyfikatorow na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	GraphCascadeOfClassifier(const ClassifierParameters& parameters)
	{
		this->parameters = parameters;

		this->boostingType = this->parameters.boostingType;
		this->maxBoostingStages = this->parameters.boostingStages;
		this->K = this->parameters.cascadeStages;
		this->A = this->parameters.maxFAR;
		this->D = this->parameters.minSpecificity;
		this->learningMethod = this->parameters.learningMethod;

		ais = new double[K];
		dis = new double[K];
		thresholds = new double[K];
		stages = new Boosting *[K];
		featurePerStage = new int*[K];
		featuresCounts = new int[K];

		ForceSeeds = this->parameters.forceSeeds;
		ValidSeed1 = this->parameters.validSeed1;
		ValidSeed2 = this->parameters.validSeed2;
		TrainSeed1 = this->parameters.trainSeed1;
		TrainSeed2 = this->parameters.trainSeed2;

		ResizeSetsWhenResampling = this->parameters.resizeSetsWhenResampling;
		ResamplingMaxValSize[0] = this->parameters.resamplingMaxValSize;
		ResamplingMaxTrainSize[0] = this->parameters.resamplingMaxTrainSize1;
		ResamplingMaxTrainSize[1] = this->parameters.resamplingMaxTrainSize2;
	}

	/// <summary>Zaladowanie kaskady klasyfikatorow z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	GraphCascadeOfClassifier(string path) { loadModel(path); }

	/// <summary>Zaladowanie kaskady klasyfikatorow z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	GraphCascadeOfClassifier(ifstream& input) { loadModel(input); }

	/// <summary>Zaladowanie kaskady klasyfikatorow z podanego strumienia oraz zapisanie parametrow w odpowiedniej strukturze</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'params'>Struktura z parametrami dla klasyfiaktora</param>
	GraphCascadeOfClassifier(ifstream& input, ClassifierParameters& params) { loadModel(input, params); }

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "ClassifierCascade";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca instacje boostingowego klasyfikatora</summary>
	/// <returns>Instacja boostingowego klasyfikatora</returns>
	Boosting* InitalizeBoostedClassifier()
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost(parameters);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost(parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	Boosting* InitalizeBoostedClassifier(Boosting* toCopy)
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost((AdaBoost*)toCopy);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost((RealBoost*)toCopy);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	Boosting* InitalizeBoostedClassifier(ifstream& input)
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost(input, parameters);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost(input, parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <param name = 'full'>Pe?ny/Skrócony opis klasyfikatora</param>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";

		text += "Used boosting type: " + boostingType + "\r\n";
		text += "Learning method: " + learningMethod + "\r\n";
		text += "Stages count: " + to_string(stagesCount) + "\r\n";
		text += "Max FAR: " + to_string(A) + "\r\n";
		text += "Min sensitivity: " + to_string(D) + "\r\n";

		for (int i = 0; i < stagesCount; i++)
		{
			text += "Stage " + to_string(i) + ":\r\n";
			text += "Threshold " + to_string(thresholds[i]) + ":\r\n";
			text += "Boosting stages: " + to_string(stages[i]->getStagesNumber()) + "\r\n";
		}

		text += "\r\n";
		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		if (thresholds != nullptr)
		{
			for (int i = 0; i < stagesCount; i++)
				delete stages[i];
			delete[] stages;
			delete[] thresholds;
			for (int i = 0; i < stagesCount; i++)
				delete[] featurePerStage[i];
			delete[] featurePerStage;
			delete[] featuresCounts;
			delete[] features;
			delete[] ais;
			delete[] dis;
		}

		string fieldName, type;
		skipHeader(input);
		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> stagesCount;
			input >> fieldName >> boostingType;
			input >> fieldName >> maxBoostingStages;
			input >> fieldName >> Ev;
			input >> fieldName >> Ex;
			input >> fieldName >> A;
			input >> fieldName >> D;
			input >> fieldName >> K;
			input >> fieldName >> featuresCount;

			features = new int[featuresCount];
			featuresCounts = new int[K];
			featurePerStage = new int*[K];
			ais = new double[K];
			dis = new double[K];

			skipHeader(input);
			int k = 0;
			for (int i = 0; i < stagesCount; i++)
			{
				input >> fieldName >> featuresCounts[i];
				featurePerStage[i] = new int[featuresCounts[i]];
				for (int j = 0; j < featuresCounts[i]; j++)
				{
					input >> featurePerStage[i][j];
					features[k] = featurePerStage[i][j];
					k++;
				}
			}
			for (int i = stagesCount; i < K; i++)
				featurePerStage[i] = nullptr;

			thresholds = new double[K];
			stages = new Boosting *[K];
			skipHeader(input);
			for (int i = 0; i < stagesCount; i++)
			{
				skipHeader(input);
				input >> fieldName >> thresholds[i];
				stages[i] = InitalizeBoostedClassifier(input);
			}
			for (int i = stagesCount; i < K; i++)
				stages[i] = nullptr;

			skipHeader(input);
			input >> fieldName >> learningMethod;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;

		parameters.cascadeStages = K;
		parameters.maxFAR = A;
		parameters.minSpecificity = D;
		parameters.boostingStages = maxBoostingStages;
		strncpy_s(parameters.boostingType, ClassifierParameters::STRING_BUFFER, boostingType.c_str(), _TRUNCATE);
		strncpy_s(parameters.learningMethod, ClassifierParameters::STRING_BUFFER, learningMethod.c_str(), _TRUNCATE);
	}


	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'param'>Struktura z parametrami dla klasyfikatora</param>
	void loadModel(ifstream& input, ClassifierParameters& params) override
	{
		loadModel(input);

		// TODO Get weak classifier and boosting parameters

		params.cascadeStages = K;
		params.maxFAR = A;
		params.minSpecificity = D;
		params.boostingStages = maxBoostingStages;
		strncpy_s(params.boostingType, ClassifierParameters::STRING_BUFFER, boostingType.c_str(), _TRUNCATE);
		strncpy_s(params.learningMethod, ClassifierParameters::STRING_BUFFER, learningMethod.c_str(), _TRUNCATE);
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Stages: " << stagesCount << endl;
		output << "Boosting_Type: " << boostingType << endl;
		output << "Boosting_Stages: " << maxBoostingStages << endl;
		output << "Ex(s): " << Ev << endl;
		output << "Ex(f): " << Ex << endl;
		output << "A: " << A << endl;
		output << "D: " << D << endl;
		output << "K: " << K << endl;
		output << "Total_features: " << featuresCount << endl;
		createSecondaryHeader(output, "Features:");
		for (int i = 0; i < stagesCount; i++)
		{
			output << "Features_Count: " << featuresCounts[i] << endl;
			for (int j = 0; j < featuresCounts[i]; j++)
				output << featurePerStage[i][j] << " ";
			output << endl;
		}
		createSecondaryHeader(output, "Boosted_Classifiers:");
		for (int i = 0; i < stagesCount; i++)
		{
			createSecondaryHeader(output, "Boosted_Classifier_" + to_string(i));
			output << "Threshold: " << thresholds[i] << endl;
			stages[i]->saveModel(output);
		}
		createSecondaryHeader(output, "Additional_Info:");
		output << "Training_method: " << learningMethod << endl;
		output << "Use_graphs: " << true << endl;
		output << "Is_uniform: " << parameters.isUniform << endl;
		output << "Childs: " << parameters.childsCount << endl;
		output << "Splits: " << parameters.splits << endl;
		createSecondaryHeader(output, "Resampling_Settings:");
		output << "Extractor: " << parameters.extractorType << endl;
		if (parameters.extractorType == HaarExtractor::GetType())
		{
			output << "Templates: " << parameters.t << endl;
			output << "Scales: " << parameters.s << endl;
			output << "Positions: " << parameters.ps << endl;
		}
		else if (parameters.extractorType == HOGExtractor::GetType())
		{
			output << "Bins: " << parameters.b << endl;
			output << "Blocks (X): " << parameters.nx << endl;
			output << "Blocks (Y): " << parameters.ny << endl;
		}
		else
		{
			output << "Harmonic: " << parameters.p << endl;
			output << "Degree: " << parameters.q << endl;
			output << "Rings: " << parameters.r << endl;
			output << "Rings_type: " << parameters.rt << endl;
			output << "Width: " << parameters.d << endl;
			output << "Overlap: " << parameters.w << endl;
		}
		output << "Resampling_scales: " << parameters.resScales << endl;
		output << "Scaling_factor: " << parameters.scaleFactor << endl;
		output << "Min_window_size: " << parameters.minWindow << endl;
		output << "Jumping_factor: " << parameters.jumpingFactor << endl;
		output << "Repetition_per_image: " << parameters.repetitionPerImage << endl;
		if (ais != nullptr)
		{
			createSecondaryHeader(output, "Validation_Scores:");
			output << "ai: ";
			for (int i = 0; i < stagesCount; i++)
				output << ais[i] << " ";
			output << endl;
			output << "di: ";
			for (int i = 0; i < stagesCount; i++)
				output << dis[i] << " ";
			output << endl;
		}
	}

	void saveModelOld(ofstream& output) const override
	{
		output << "ClassifierCascadeVJ_v2" << endl;
		output << Ev << endl;
		output << A << endl;
		output << 0 << endl;
		output << D << endl;
		output << 0 << endl;
		output << K << endl;
		output << stagesCount << endl;
		output << boostingType << endl;
		for (int i = 0; i < (int)stagesCount; i++)
		{
			output << thresholds[i] << endl;
			stages[i]->saveModelOld(output);
		}
	}

	class Stage
	{
	public:
		string id = "";
		static string errfilename;


		ClassifierParameters parameters;
		int maxBoostingStages = 100;


		double* thresholds = nullptr; /// <summary>Progi dla klasyfikatorow</summary>
		Boosting** stages = nullptr; /// <summary>Wybrane w wyniku nauki klasyfiaktory (boostingowe)</summary>
		string boostingType = RealBoost::GetType(); /// <summary>Typ algorytmu boostingowego</summary>

		double* ais = nullptr;
		double* dis = nullptr;

		int level = 0;
		int childsCount = 3; // wieksze rowne 1 po przekroczeniu lastSplitLevel bedzie zawsze rowne 1, musi być nieparzyste
		int splits = 1; // wieksze rowne 1

		bool classicApproach = false;

		int scales;
		int minWindow;
		double scalingRatio;
		double jumpingRatio;
		int repetitionsPerImage;

		double* scalesProbability = nullptr;

		int K;
		double Amax, Dmin;
		double ai = 1, di = 0;
		double Acurr, Dcurr;
		double Ev;

		bool forceSeeds, resizeSetsWhenResampling;
		long seedVal, seedTrain, seedVStep, seedTStep;
		int ResamplingMaxValSize[1], ResamplingMaxTrainSize[2];

		Stage* parent = nullptr;
		Stage** childs = nullptr;

		Stage(int maxStages, ClassifierParameters parameters)
		{
			id = "0";
			this->parameters = parameters;

			// Inicjalizacja prawdopobienstw dla skal oraz rozmiarow okien
			scales = parameters.resScales;
			minWindow = parameters.minWindow;
			scalingRatio = parameters.scaleFactor;
			jumpingRatio = parameters.jumpingFactor;
			repetitionsPerImage = parameters.repetitionPerImage;

			scalesProbability = new double[scales];
			double* windows = new double[scales];
			double windowSum = 1;
			windows[0] = 1;
			for (int s = 1; s < scales; s++)
			{
				windows[s] = windows[s - 1] * pow(1.0 / scalingRatio, 2);
				windowSum += windows[s];
			}
			for (int s = 0; s < scales; s++)
			{
				scalesProbability[s] = windows[s] / windowSum;
			}
			delete[] windows;

			resizeSetsWhenResampling = parameters.resizeSetsWhenResampling;
			ResamplingMaxValSize[0] = parameters.resamplingMaxValSize;
			ResamplingMaxTrainSize[0] = parameters.resamplingMaxTrainSize1;
			ResamplingMaxTrainSize[1] = parameters.resamplingMaxTrainSize2;

			parent = nullptr;
			classicApproach = true;

			maxBoostingStages = maxStages;

			Amax = parameters.maxFAR;
			Dmin = parameters.minSpecificity;
			K = parameters.cascadeStages;
			boostingType = parameters.boostingType;

			Acurr = 1;
			Dcurr = 1;
			Ev = 0;

			level = 0;
			childsCount = parameters.childsCount;
			splits = parameters.splits;

			forceSeeds = parameters.forceSeeds;
			seedVal = parameters.validSeed1 + parameters.validSeed2;
			seedTrain = parameters.trainSeed1 + parameters.trainSeed2;
			seedVStep = parameters.validSeed2;
			seedTStep = parameters.trainSeed2;
		}

		Stage(Stage* parentStage)
		{
			this->parent = parentStage;

			scales = parentStage->scales;
			minWindow = parentStage->minWindow;
			scalingRatio = parentStage->scalingRatio;
			jumpingRatio = parentStage->jumpingRatio;
			repetitionsPerImage = parentStage->repetitionsPerImage;
			scalesProbability = new double[scales];
			for (int s = 0; s < scales; s++)
				scalesProbability[s] = parentStage->scalesProbability[s];

			maxBoostingStages = parentStage->maxBoostingStages;
			boostingType = parentStage->boostingType;
			parameters = parentStage->parameters;

			resizeSetsWhenResampling = parentStage->resizeSetsWhenResampling;
			ResamplingMaxValSize[0] = parentStage->ResamplingMaxValSize[0];
			ResamplingMaxTrainSize[0] = parentStage->ResamplingMaxTrainSize[0];
			ResamplingMaxTrainSize[1] = parentStage->ResamplingMaxTrainSize[1];

			Amax = parentStage->Amax;
			Dmin = parentStage->Dmin;
			K = parentStage->K;

			Acurr = parentStage->Acurr;
			Dcurr = parentStage->Dcurr;
			Ev = parentStage->Ev;

			level = parentStage->level + 1;
			splits = parentStage->splits;
			if (level >= splits)
				childsCount = 1;
			else
				childsCount = parentStage->childsCount;

			forceSeeds = parentStage->forceSeeds;
			seedVStep = parentStage->seedVStep;
			seedTStep = parentStage->seedTStep;
			seedVal = parentStage->seedVal + seedVStep;
			seedTrain = parentStage->seedTrain + seedTStep;

			stages = new Boosting *[level];
			thresholds = new double[level];
			ais = new double[level];
			dis = new double[level];

			for (int i = 0; i < level - 1; i++)
			{
				stages[i] = parentStage->stages[i];
				thresholds[i] = parentStage->thresholds[i];
				ais[i] = parentStage->ais[i];
				dis[i] = parentStage->dis[i];
			}
			stages[level - 1] = nullptr;
		}

		~Stage()
		{
			if (childs != nullptr)
			{
				for (int i = 0; i < childsCount; i++)
					delete childs[i];
				delete[] childs;
			}
			delete[] scalesProbability;

			if (level > 0)
			{
				delete[] ais;
				delete[] dis;
				delete[] thresholds;

				delete stages[level - 1];
				delete[] stages;
			}
		}

		double train(const double* const* Xtrain, const int* Dtrain, const double* const* Xvalidate, const int* Dvalidate, int trainigSamples, int validationSamples, int attribiutesCount,
			Extractor* ext, vector<string>& imageList, int posEndIndexValidate, int posEndIndexTrain, int canDeleteAfterVal, int canDeleteAfterTrain, double bestEV = INFINITY)
		{
			// uncomment for Full Tree
		    // bestEV = INFINITY

			if (Acurr > Amax)
			{
				ofstream errfile;
				errfile.open(errfilename + "_lvl_" + to_string(level) + "_id_" + id + ".txt");

				errfile << "A_curr: " << Acurr << endl;
				errfile << "D_curr: " << Dcurr << endl;
				errfile << "childs: " << childsCount << endl;
				errfile << "train: " << trainigSamples << endl;
				errfile << "validate: " << validationSamples << endl;

				std::chrono::system_clock::time_point beginTrainGr = std::chrono::system_clock::now();

				// Utworzenie dzieci
				childs = new Stage *[childsCount];
				for (int i = 0; i < childsCount; i++)
				{
					childs[i] = new Stage(this);
					childs[i]->id = this->id + "_" + to_string(i);
					childs[i]->classicApproach = false;
					errfile << endl << "childCreated  " << i << endl;
				}
				int middleChild = ((childsCount - 1) / 2); // indeksowane od 0

				if (classicApproach == true)
					childs[middleChild]->classicApproach = true;

				errfile << "middleChild: " << middleChild << endl;

				// Ustalenie wymagan, srodkowy wezel zawwsze idzie algorytmem VJ, pozostale wskazanym
				bool isUGMG = strcmp("UGM-G", parameters.learningMethod) == 0;
				bool isUGM = strcmp("UGM", parameters.learningMethod) == 0;

				double a = pow(Amax, 1.0 / K);
				double d = pow(Dmin, 1.0 / K);
				if (isUGMG && (!classicApproach || parameters.isUniform))
				{
					a = Amax / (Acurr * pow(a, K - level - 1));
					d = Dmin / (Dcurr * pow(d, K - level - 1));
				}
				else if (isUGM && (!classicApproach || parameters.isUniform))
				{
					a = pow(Amax / Acurr, 1.0 / (K - level));
					d = pow(Dmin / Dcurr, 1.0 / (K - level));
				}

				errfile << "a: " << a << endl;
				errfile << "d: " << d << endl;

				// Przygotowanie boostingowego klasyfikatora na danym etapie
				Boosting* bClassifier = InitalizeBoostedClassifier();
				double* clsThresholds = new double[maxBoostingStages];
				double* fars = new double[maxBoostingStages];
				double* sens = new double[maxBoostingStages];

				// Nauka dla srodkowego dziecka
				double clsThreshold;
				double* weights = nullptr;
				int bStages = 0;
				ai = INFINITY, di = INFINITY;

				errfile << endl << "maxBoostingStage: " << maxBoostingStages << endl;
				int* outputOrder = new int[posEndIndexValidate];
				int* indices = new int[trainigSamples];
				for (int i = 0; i < trainigSamples; i++)
					indices[i] = i;
				bClassifier->initializeData(Xtrain, indices, trainigSamples, attribiutesCount, trainigSamples);
				while (ai > a&& bStages < maxBoostingStages)
				{
					// Dodanie slabego klasyfikatora
					bStages++;
					errfile << endl << "bStages: " << bStages << endl;

					bClassifier->addStage(Xtrain, Dtrain, indices, weights, trainigSamples, attribiutesCount, trainigSamples);
					// Wyznaczenie wyjsc dla proby walidujacej
					double* clsOutput = bClassifier->calculateOutput(Xvalidate, validationSamples, attribiutesCount);

					// Posortowanie wyjsc dla probek pozytwynych w celu wyznaczenia czulosci
					for (int i = 0; i < posEndIndexValidate; i++)
						outputOrder[i] = i;
					sort_indexes(clsOutput, outputOrder, posEndIndexValidate);

					// Wybor progu zapewniajacego zadana czulosc
					clsThreshold = clsOutput[outputOrder[(int)floor((posEndIndexValidate) * (1 - d))]];

					// Klasyfikacja probek z zadanym progiem
					int TP = 0, TN = 0, FP = 0, FN = 0;
					for (int s = 0; s < validationSamples; s++)
					{
						int Y = clsOutput[s] >= clsThreshold ? 1 : -1;
						if (Dvalidate[s] == Y && Dvalidate[s] == 1)
							TP++;
						else if (Dvalidate[s] == Y && Dvalidate[s] == -1)
							TN++;
						else if (Dvalidate[s] != Y && Dvalidate[s] == 1 && Y == -1)
							FN++;
						else
							FP++;
					}
					ai = 1.0 * (FP) / (FP + TN);
					di = 1.0 * (TP) / (TP + FN);

					clsThresholds[bStages - 1] = clsThreshold;
					sens[bStages - 1] = di;
					fars[bStages - 1] = ai;

					errfile << "ai: " << ai << endl;
					errfile << "di: " << di << endl;
					errfile << "thr: " << clsThreshold << endl;

					delete[] clsOutput;
					if (di < d)
					{
						delete[] weights;
						delete[] outputOrder;
						delete[] clsOutput;
						delete[] indices;

						throw ERRORS::UNKNOWN_ERROR;
					}
				}
				bClassifier->endStagewiseTraining(indices, attribiutesCount, trainigSamples);

				childs[middleChild]->stages[level] = bClassifier;
				childs[middleChild]->thresholds[level] = clsThreshold;
				childs[middleChild]->Ev += bClassifier->getFeauresCount() * childs[middleChild]->Acurr;
				childs[middleChild]->Acurr *= ai;
				childs[middleChild]->Dcurr *= di;
				childs[middleChild]->ais[level] = ai;
				childs[middleChild]->dis[level] = di;

				errfile << endl << "middle" << endl;
				errfile << "ai: " << ai << endl;
				errfile << "di: " << di << endl;
				errfile << "acurr: " << childs[middleChild]->Acurr << endl;
				errfile << "dcurr: " << childs[middleChild]->Dcurr << endl;
				errfile << "thr: " << clsThreshold << endl;
				errfile << "Fet: " << bClassifier->getFeauresCount() << endl;
				errfile << "Ev: " << childs[middleChild]->Ev << endl;
				errfile << "cls: " << endl;
				errfile << bClassifier->toString() << endl;
				//childs[middleChild]->classifiers[childs[middleChild]->classifiers.size() - 1]->saveModel(errfile);

				Boosting* cls = bClassifier;
				for (int i = middleChild - 1; i >= 0; i--)
				{
					bStages--;
					cls = InitalizeBoostedClassifier(cls);
					cls->removeStage();

					childs[i]->stages[level] = cls;
					childs[i]->thresholds[level] = clsThresholds[bStages - 1];
					childs[i]->Ev += cls->getFeauresCount() * childs[i]->Acurr;
					childs[i]->Acurr *= fars[bStages - 1];
					childs[i]->Dcurr *= sens[bStages - 1];
					childs[i]->ais[level] = fars[bStages - 1];
					childs[i]->dis[level] = sens[bStages - 1];

					errfile << endl << "left " << i << endl;
					errfile << "ai: " << fars[bStages - 1] << endl;
					errfile << "di: " << sens[bStages - 1] << endl;
					errfile << "acurr: " << childs[i]->Acurr << endl;
					errfile << "dcurr: " << childs[i]->Dcurr << endl;
					errfile << "thr: " << clsThresholds[bStages - 1] << endl;
					errfile << "Fet: " << cls->getFeauresCount() << endl;
					errfile << "Ex: " << childs[i]->Ev << endl;
					errfile << "cls: " << endl;
					errfile << cls->toString() << endl;
					//childs[i]->classifiers[childs[i]->classifiers.size() - 1]->saveModel(errfile);
				}

				cls = bClassifier;
				for (int i = middleChild + 1; i < childsCount; i++)
				{
					cls = InitalizeBoostedClassifier(cls);
					cls->initializeData(Xtrain, indices, trainigSamples, attribiutesCount, trainigSamples);
					cls->addStage(Xtrain, Dtrain, indices, weights, trainigSamples, attribiutesCount, trainigSamples);

					double* clsOutput = cls->calculateOutput(Xvalidate, validationSamples, attribiutesCount);

					// Posortowanie wyjsc dla probek pozytwynych w celu wyznaczenia czulosci
					for (int i = 0; i < posEndIndexValidate; i++)
						outputOrder[i] = i;
					sort_indexes(clsOutput, outputOrder, posEndIndexValidate);

					// Wybor progu zapewniajacego zadana czulosc
					clsThreshold = clsOutput[outputOrder[(int)floor((posEndIndexValidate) * (1 - d))]];

					// Klasyfikacja probek z zadanym progiem
					int TP = 0, TN = 0, FP = 0, FN = 0;
					for (int s = 0; s < validationSamples; s++)
					{
						int Y = clsOutput[s] >= clsThreshold ? 1 : -1;
						if (Dvalidate[s] == Y && Dvalidate[s] == 1)
							TP++;
						else if (Dvalidate[s] == Y && Dvalidate[s] == -1)
							TN++;
						else if (Dvalidate[s] != Y && Dvalidate[s] == 1 && Y == -1)
							FN++;
						else
							FP++;
					}
					ai = 1.0 * (FP) / (FP + TN);
					di = 1.0 * (TP) / (TP + FN);

					//clsThresholds[bStages - 1] = clsThreshold;
					//sens[bStages - 1] = di;
					//fars[bStages - 1] = ai;

					cls->endStagewiseTraining(indices, attribiutesCount, trainigSamples);
					delete[] clsOutput;

					childs[i]->stages[level] = cls;
					childs[i]->thresholds[level] = clsThreshold;
					childs[i]->Ev += cls->getFeauresCount() * childs[i]->Acurr;
					childs[i]->Acurr *= ai;
					childs[i]->Dcurr *= di;
					childs[i]->ais[level] = ai;
					childs[i]->dis[level] = di;

					errfile << endl << "right " << i << endl;
					errfile << "ai: " << ai << endl;
					errfile << "di: " << di << endl;
					errfile << "acurr: " << childs[i]->Acurr << endl;
					errfile << "dcurr: " << childs[i]->Dcurr << endl;
					errfile << "thr: " << clsThreshold << endl;
					errfile << "Fet: " << cls->getFeauresCount() << endl;
					errfile << "Ex: " << childs[i]->Ev << endl;
					errfile << "cls: " << endl;
					errfile << cls->toString() << endl;
					//childs[i]->classifiers[childs[i]->classifiers.size() - 1]->saveModel(errfile);
				}
				std::chrono::system_clock::time_point endTrainGr = std::chrono::system_clock::now();
				errfile << "Traintime difference = " << std::chrono::duration_cast<std::chrono::nanoseconds>(endTrainGr - beginTrainGr).count() << "ns" << endl;
				errfile << "Traintime difference = " << std::chrono::duration_cast<std::chrono::milliseconds> (endTrainGr - beginTrainGr).count() << "ms" << endl;

				delete[] indices;
				delete[] outputOrder;
				delete[] weights;
				delete[] clsThresholds;
				delete[] fars;
				delete[] sens;

				// Resampling  
				if (level + 1 < K)
				{
					std::chrono::system_clock::time_point beginResamplingGr = std::chrono::system_clock::now();

					int* scalesNumbers = new int[repetitionsPerImage];
					int* xs = new int[repetitionsPerImage];
					int* ys = new int[repetitionsPerImage];
					int* wxs = new int[repetitionsPerImage];
					int* wys = new int[repetitionsPerImage];
					const double** featuresList = new const double*[repetitionsPerImage];
					int fc = 0;

					errfile << "-------------------" << endl;
					errfile << "val size: " << validationSamples << endl;
					errfile << "train size: " << trainigSamples << endl;
					errfile << "-------------------" << endl;

					// Usuwanie probek oraz przygtowanie kopi cech o ile nie spelniono Acurr < Amax i dodanie do tablicy indices klasyfikatorow ktore potrzebuja doprobkowania
					// sprawdzenie ktore z dzieci nie spelnily wymagan
					vector<int> IndicesTrain, IndicesVal;
					for (int c = 0; c < childsCount; c++)
					{
						double predEv = childs[c]->Ev + childs[c]->Acurr * childs[c]->stages[level]->getFeauresCount() * parameters.pruningFactor;
						if (childs[c]->Acurr > Amax&& predEv < bestEV)
						{
							IndicesTrain.push_back(c);
							IndicesVal.push_back(c);

							errfile << "Child " << to_string(c) + " A: " << to_string(childs[c]->Acurr) + " > " + to_string(Amax) + " EX: " << to_string(childs[c]->Ev) + " < " + to_string(bestEV) << endl;
						}
						else if (childs[c]->Ev > bestEV)
							errfile << "Child " << to_string(c) + " EX: " << to_string(childs[c]->Ev) + " > " + to_string(bestEV) << endl;

					}
					errfile << "child to resample: " << IndicesTrain.size() << endl;

					// Doprobkowanie negatyow dla zbioru walidujacego, gdy nie spelniono Acuur < Amax
					const double*** XsValidate = new const double**[childsCount];
					int** DsValidate = new int*[childsCount];
					int* ValPosEnds = new int[childsCount];
					int* ValSamplesCounts = new int[childsCount];
					int* ValCanDelete = new int[childsCount];

					for (int c = 0; c < childsCount; c++)
					{
						XsValidate[c] = nullptr;
						DsValidate[c] = nullptr;
					}

					// skopiowanie negatywow oraz pozytywow sklasyfikowanych jako 1 dla i-tego dziecka
					int sampleLimit = resizeSetsWhenResampling ? ResamplingMaxValSize[0] : validationSamples;
					for (int ind = 0; ind < IndicesVal.size(); ind++)
					{
						int cInd = IndicesVal[ind];
						XsValidate[cInd] = new const double*[sampleLimit];
						DsValidate[cInd] = new int[sampleLimit];

						Boosting* boostCls = childs[cInd]->stages[level];
						double cascadeThr = childs[cInd]->thresholds[level];

						int valAdded = 0, t = 0;
						for (; t < posEndIndexValidate; t++)
						{
							if (boostCls->classify(Xvalidate[t], attribiutesCount, cascadeThr) == 1)
							{
								XsValidate[cInd][valAdded] = Xvalidate[t];
								DsValidate[cInd][valAdded] = Dvalidate[t];
								valAdded++;
							}
						}
						ValPosEnds[cInd] = valAdded;

						for (; t < canDeleteAfterVal && valAdded < sampleLimit; t++)
						{
							if (boostCls->classify(Xvalidate[t], attribiutesCount, cascadeThr) == 1)
							{
								XsValidate[cInd][valAdded] = Xvalidate[t];
								DsValidate[cInd][valAdded] = Dvalidate[t];
								valAdded++;
							}
						}
						if (childsCount == 1)
							ValCanDelete[cInd] = valAdded;
						else
							ValCanDelete[cInd] = sampleLimit;

						t = canDeleteAfterVal;
						for (; t < validationSamples && valAdded < sampleLimit; t++)
						{
							if (boostCls->classify(Xvalidate[t], attribiutesCount, cascadeThr) == 1)
							{
								XsValidate[cInd][valAdded] = Xvalidate[t];
								DsValidate[cInd][valAdded] = Dvalidate[t];
								valAdded++;
							}
							else
								delete[] Xvalidate[t];
						}
						ValSamplesCounts[cInd] = valAdded;

						for (; t < validationSamples; t++)
							delete[] Xvalidate[t];

						errfile << "-------------------" << endl;
						errfile << "child: " << IndicesVal[ind] << " validate samples after removing " << ValSamplesCounts[cInd] << endl;
						errfile << "child: " << IndicesVal[ind] << " validate positive samples after removing " << ValPosEnds[cInd] << endl;
					}
					validationSamples = sampleLimit;

					for (int ind = (int)IndicesVal.size() - 1; ind >= 0; ind--)
					{
						int cInd = IndicesVal[ind];
						if (ValSamplesCounts[cInd] >= validationSamples)
							IndicesVal.erase(IndicesVal.begin() + ind);
					}

					vector<const double*> newValSamplesToDelete;
					if (childsCount > 1)
						newValSamplesToDelete.reserve(validationSamples);

					errfile << "rep per image: " << repetitionsPerImage << endl;
					for (int s = scales - 1; s >= 0; s--)
						errfile << "scale " << s << " prob: " << scalesProbability[s] << endl;

					if (forceSeeds)
					{
						srand(seedVal);
						errfile << "seed val: " << seedVal << endl;
					}
					while (IndicesVal.size() > 0)
					{
						// zaladowanie obrazu i dodanie nowych probek
						// zaladowanie obrazu i dodanie nowych probek
						int currentResamplingImage = rand() % (imageList.size());

						ext->loadImageData(imageList[currentResamplingImage]);
						int nx = ext->getWidth(), ny = ext->getHeight();

						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							// losownie skali
							double scaleTest = static_cast <double> (rand()) / static_cast <double> (RAND_MAX);
							int scaleNumber = 0;
							double threshold = 0;
							for (int s = scales - 1; s >= 0; s--)
							{
								if (scaleTest < scalesProbability[s] + threshold)
								{
									scaleNumber = s;
									break;
								}
								threshold += scalesProbability[s];
							}

							int wx = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
							int wy = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
							if (wx > nx) wx = nx;
							if (wy > ny) wy = ny;
							wx = wy = min(wx, wy);
							if (wx % 2 == 1)
								wx = wy = wx - 1;

							int x = rand() % (nx - wx);
							int y = rand() % (ny - wy);

							scalesNumbers[rep] = scaleNumber;
							xs[rep] = x;
							ys[rep] = y;
							wxs[rep] = wx;
							wys[rep] = wy;
						}

						// jak wyzej, chociaz sekcja krytyczna moze byc tu za duza
#pragma omp parallel for num_threads(OMP_NUM_THR)
						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							tie(fc, featuresList[rep]) = ext->extractFromWindow(wxs[rep], wys[rep], xs[rep], ys[rep]);
						}

						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							bool append = false;
							for (int ind = (int)IndicesVal.size() - 1; ind >= 0; ind--)
							{
								int cInd = IndicesVal[ind];

								//dodanie probek do klasyfikatorow jesli == 1
								if (classify(featuresList[rep], fc, childs[cInd]) == 1)
								{
									int valAdded = ValSamplesCounts[cInd];
									XsValidate[cInd][valAdded] = featuresList[rep];
									DsValidate[cInd][valAdded] = -1;
									ValSamplesCounts[cInd]++;
									append = true;
								}

								// jesli uzupelniono probki dla i-tego klasyfikatora zosaje usuniety z listy do uzupelniea
								if (ValSamplesCounts[cInd] == validationSamples)
								{
									errfile << "-------------------" << endl;
									errfile << "child: " << IndicesVal[ind] << " validate samples after resampling " << ValSamplesCounts[cInd] << endl;

									IndicesVal.erase(IndicesVal.begin() + ind);
								}
							}
							if (!append)
								delete[] featuresList[rep];
							else if (append && childsCount > 1)
								newValSamplesToDelete.push_back(featuresList[rep]);
						}
						ext->clearImageData();
					}

					// Doprobkowanie negatywow dla zbioru uczacego, gdy nie spelniono Acuur < Amax
					const double*** XsTrain = new const double**[childsCount];
					int** DsTrain = new int*[childsCount];
					int* TrainSamplesCounts = new int[childsCount];
					int* TrainCanDelete = new int[childsCount];

					for (int c = 0; c < childsCount; c++)
					{
						XsTrain[c] = nullptr;
						DsTrain[c] = nullptr;
					}

					// skopiowanie negatywow sklasyfikowanych jako 1 oraz wszystkich pozytywow dla i-tego dziecka
					int trSamples = level == 0 ? ResamplingMaxTrainSize[0] : ResamplingMaxTrainSize[1];
					int sampleLimitTr = resizeSetsWhenResampling ? trSamples : trainigSamples;
					for (int ind = 0; ind < IndicesTrain.size(); ind++)
					{
						int cInd = IndicesTrain[ind];
						XsTrain[cInd] = new const double*[sampleLimitTr];
						DsTrain[cInd] = new int[sampleLimitTr];

						Boosting* boostCls = childs[cInd]->stages[level];
						double cascadeThr = childs[cInd]->thresholds[level];

						int trainAdded = 0, t = 0;
						for (; t < posEndIndexTrain; t++)
						{
							XsTrain[cInd][trainAdded] = Xtrain[t];
							DsTrain[cInd][trainAdded] = Dtrain[t];
							trainAdded++;
						}

						for (; t < canDeleteAfterTrain && trainAdded < sampleLimitTr; t++)
						{
							if (boostCls->classify(Xtrain[t], attribiutesCount, cascadeThr) == 1)
							{
								XsTrain[cInd][trainAdded] = Xtrain[t];
								DsTrain[cInd][trainAdded] = Dtrain[t];
								trainAdded++;
							}
						}
						if (childsCount == 1)
							TrainCanDelete[cInd] = trainAdded;
						else
							TrainCanDelete[cInd] = sampleLimitTr;

						t = canDeleteAfterTrain;
						for (; t < trainigSamples && trainAdded < sampleLimitTr; t++)
						{
							if (boostCls->classify(Xtrain[t], attribiutesCount, cascadeThr) == 1)
							{
								XsTrain[cInd][trainAdded] = Xtrain[t];
								DsTrain[cInd][trainAdded] = Dtrain[t];
								trainAdded++;
							}
							else
								delete[] Xtrain[t];
						}
						TrainSamplesCounts[cInd] = trainAdded;

						for (; t < trainigSamples; t++)
							delete[] Xtrain[t];

						errfile << "-------------------" << endl;
						errfile << "child: " << IndicesTrain[ind] << " train samples after removing " << TrainSamplesCounts[cInd] << endl;
					}
					trainigSamples = sampleLimitTr;

					for (int ind = (int)IndicesTrain.size() - 1; ind >= 0; ind--)
					{
						int cInd = IndicesTrain[ind];
						if (TrainSamplesCounts[cInd] >= trainigSamples)
							IndicesTrain.erase(IndicesTrain.begin() + ind);
					}

					vector<const double*> newTrainSamplesToDelete;
					if (childsCount > 1)
						newTrainSamplesToDelete.reserve(trainigSamples);

					if (forceSeeds)
					{
						srand(seedTrain);
						errfile << "seed train: " << seedTrain << endl;
					}
					while (IndicesTrain.size() > 0)
					{
						// zaladowanie obrazu i dodanie nowych probek
						int currentResamplingImage = rand() % (imageList.size());
						vector<double> features;

						ext->loadImageData(imageList[currentResamplingImage]);
						int nx = ext->getWidth(), ny = ext->getHeight();

						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							// losownie skali
							double scaleTest = static_cast <double> (rand()) / static_cast <double> (RAND_MAX);
							int scaleNumber = 0;
							double threshold = 0;
							for (int s = scales - 1; s >= 0; s--)
							{
								if (scaleTest < scalesProbability[s] + threshold)
								{
									scaleNumber = s;
									break;
								}
								threshold += scalesProbability[s];
							}

							int wx = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
							int wy = (int)round(pow(scalingRatio, scaleNumber) * minWindow);
							if (wx > nx) wx = nx;
							if (wy > ny) wy = ny;
							wx = wy = min(wx, wy);
							if (wx % 2 == 1)
								wx = wy = wx - 1;

							int x = rand() % (nx - wx);
							int y = rand() % (ny - wy);

							scalesNumbers[rep] = scaleNumber;
							xs[rep] = x;
							ys[rep] = y;
							wxs[rep] = wx;
							wys[rep] = wy;
						}

						// jak wyzej
#pragma omp parallel for num_threads(OMP_NUM_THR)
						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							tie(fc, featuresList[rep]) = ext->extractFromWindow(wxs[rep], wys[rep], xs[rep], ys[rep]);
						}

						for (int rep = 0; rep < repetitionsPerImage; rep++)
						{
							bool append = false;
							for (int ind = (int)IndicesTrain.size() - 1; ind >= 0; ind--)
							{
								int cInd = IndicesTrain[ind];

								//dodanie probek do klasyfikatorow jesli == 1
								if (classify(featuresList[rep], fc, childs[cInd]) == 1)
								{
									int trainAdded = TrainSamplesCounts[cInd];
									XsTrain[cInd][trainAdded] = featuresList[rep];
									DsTrain[cInd][trainAdded] = -1;
									TrainSamplesCounts[cInd]++;
									append = true;
								}

								// jesli uzupelniono probki dla i-tego klasyfikatora zosaje usuniety z listy do uzupelniea
								if (TrainSamplesCounts[cInd] == trainigSamples)
								{
									errfile << "-------------------" << endl;
									errfile << "child: " << IndicesTrain[ind] << " train samples after resampling " << TrainSamplesCounts[cInd] << endl;

									IndicesTrain.erase(IndicesTrain.begin() + ind);
								}
							}
							if (!append)
								delete[] featuresList[rep];
							else if (append && childsCount > 1)
								newTrainSamplesToDelete.push_back(featuresList[rep]);
						}
						ext->clearImageData();
					}


					std::chrono::system_clock::time_point endResamplingGr = std::chrono::system_clock::now();
					errfile << "Resamplintime difference = " << std::chrono::duration_cast<std::chrono::nanoseconds>(endResamplingGr - beginResamplingGr).count() << "ns" << endl;
					errfile << "Resamplintime difference = " << std::chrono::duration_cast<std::chrono::milliseconds> (endResamplingGr - beginResamplingGr).count() << "ms" << endl;

					delete[] scalesNumbers;
					delete[] xs;
					delete[] ys;
					delete[] wxs;
					delete[] wys;
					delete[] featuresList;

					// wywolanie train na z resamplowanym zbiorze o ile nie speleniono wymagan	
					for (int c = 0; c < childsCount; c++)
					{
						double predEv = childs[c]->Ev + childs[c]->Acurr * childs[c]->stages[level]->getFeauresCount() * parameters.pruningFactor;
						if (childs[c]->Acurr > Amax&& predEv < bestEV)
						{
							errfile << "Child " << to_string(c) + " A: " << to_string(childs[c]->Acurr) + " > " + to_string(Amax) + " EX: " << to_string(childs[c]->Ev) + " < " + to_string(bestEV) << endl;

							double tmpEV = childs[c]->train(XsTrain[c], DsTrain[c], XsValidate[c], DsValidate[c], TrainSamplesCounts[c], ValSamplesCounts[c], attribiutesCount,
								ext, imageList, ValPosEnds[c], posEndIndexTrain, ValCanDelete[c], TrainCanDelete[c], bestEV);
							if (tmpEV < bestEV)
								bestEV = tmpEV;

							errfile << "Best EX: " << to_string(bestEV) << endl;
						}
						else if (childs[c]->Ev > bestEV)
							errfile << "Child " << to_string(c) + " EX: " << to_string(childs[c]->Ev) + " > " + to_string(bestEV) << endl;
					}

					if (childsCount > 1)
					{
						for (size_t t = 0; t < newValSamplesToDelete.size(); t++)
							delete[] newValSamplesToDelete[t];

						for (size_t t = 0; t < newTrainSamplesToDelete.size(); t++)
							delete[] newTrainSamplesToDelete[t];
					}

					delete[] ValSamplesCounts;
					delete[] TrainSamplesCounts;

					for (int t = 0; t < childsCount; t++)
					{
						delete[] XsValidate[t];
						delete[] DsValidate[t];

						delete[] XsTrain[t];
						delete[] DsTrain[t];
					}
					delete[] XsValidate;
					delete[] DsValidate;
					delete[] ValPosEnds;
					delete[] ValCanDelete;

					delete[] XsTrain;
					delete[] DsTrain;
					delete[] TrainCanDelete;
				}
				else
				{
					for (int t = canDeleteAfterVal; t < validationSamples; t++)
						delete[] Xvalidate[t];
					for (int t = canDeleteAfterTrain; t < trainigSamples; t++)
						delete[] Xtrain[t];
				}

				for (int c = 0; c < childsCount; c++)
				{
					if (childs[c]->Ev < bestEV && childs[c]->Acurr < Amax)
					{
						errfile << "Child " << to_string(c) + " A: " << to_string(childs[c]->Acurr) + " < " + to_string(Amax) + " EX: " << to_string(childs[c]->Ev) + " < " + to_string(bestEV) << endl;

						bestEV = childs[c]->Ev;

						errfile << "Best EX: " << to_string(bestEV) << endl;
					}
					else if (childs[c]->Ev > bestEV)
						errfile << "Child " << to_string(c) + " EX: " << to_string(childs[c]->Ev) + " > " + to_string(bestEV) << endl;
					else
						errfile << "Child " << to_string(c) + " !!!! A: " << to_string(childs[c]->Acurr) << "!!!!" << endl;
				}

				errfile.close();
				return bestEV;
			}
			return bestEV;
		}

	private:
		Boosting* InitalizeBoostedClassifier()
		{
			if (boostingType == AdaBoost::GetType())
				return new AdaBoost(parameters);
			else if (boostingType == RealBoost::GetType())
				return new RealBoost(parameters);
			else
				throw ERRORS::NOT_IMPLEMENTED;
		}

		Boosting* InitalizeBoostedClassifier(Boosting* toCopy)
		{
			if (boostingType == AdaBoost::GetType())
				return new AdaBoost((AdaBoost*)toCopy);
			else if (boostingType == RealBoost::GetType())
				return new RealBoost((RealBoost*)toCopy);
			else
				throw ERRORS::NOT_IMPLEMENTED;
		}

		/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
		/// <param name = 'X'>Cechy próbki do klasyfikacji</param>
		/// <returns>Odpowiedz klasyfikatora</returns>
		inline int classify(const double* X, int attribiutesCount, Stage* child) const
		{
			int yp = -1;

			for (int i = 0; i < child->level; i++)
			{
				yp = child->stages[i]->classify(X, attribiutesCount, child->thresholds[i]);

				if (yp != 1)
					return yp;
			}
			return yp;
		}
	};

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Cechy próbek ucz?cych</param>
	/// <param name = 'D'>Klasy próbek ucz?cych</param>
	/// <param name = 'Indices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	void train(const double* const* Xtrain, const int* Dtrain, const double* const* Xvalidate, const int* Dvalidate, int trainigSamples, int validationSamples, int attribiutesCount) override
	{
		if (stagesCount > 0)
		{
			for (int i = 0; i < stagesCount; i++)
				delete stages[i];
			for (int i = 0; i < stagesCount; i++)
				delete[] featurePerStage[i];
			delete[] features;
		}

		ofstream errfile;
		string errFileName = "errorlogcascadeGraph " + learningMethod + "_" + to_string(A) + " " + to_string(D) + " K" + to_string(K) + " C" + to_string(parameters.childsCount) + " S" + to_string(parameters.splits) + " " + parameters.boostingType + " " + parameters.extractorType;
		errfile.open(errFileName + ".txt");

		std::chrono::system_clock::time_point beginGr = std::chrono::system_clock::now();

		Stage::errfilename = errFileName;

		errfile << "A: " << A << endl;
		errfile << "D: " << D << endl;

		// Przygotowanie ekstraktora cech do resamplingu
		string extractorType = string(parameters.extractorType);
		int extParams[6];
		if (extractorType == HaarExtractor::GetType())
		{
			extParams[0] = parameters.t;
			extParams[1] = parameters.s;
			extParams[2] = parameters.ps;
		}
		else if (extractorType == HOGExtractor::GetType())
		{
			extParams[0] = parameters.b;
			extParams[1] = parameters.nx;
			extParams[2] = parameters.ny;
		}
		else
		{
			extParams[0] = parameters.p;
			extParams[1] = parameters.q;
			extParams[2] = parameters.r;
			extParams[3] = parameters.rt;
			extParams[4] = parameters.d;
			extParams[5] = parameters.w;
		}
		Extractor* extractor = InitializeExtractor(extractorType, extParams, SaveFileType::binary8bit);

		// Przygotowanie listy plikow do resamplingu
		string nonFaceImagesFolder = string(parameters.nonFaceImagesPath);
		vector<string> imageList;
		for (auto& p : filesystem::directory_iterator(nonFaceImagesFolder))
		{
			string path = p.path().string();
			if (path.size() > 9 && path.substr(path.size() - 9) == "gray.8bin")
				imageList.push_back(path);
		}

		errfile << "val size: " << validationSamples << endl;
		errfile << "train size: " << trainigSamples << endl;

		int positiveEndIndexValidate = 0;
		for (; positiveEndIndexValidate < validationSamples; positiveEndIndexValidate++)
			if (Dvalidate[positiveEndIndexValidate] != 1)
				break;

		int positiveEndIndexTrain = 0;
		for (; positiveEndIndexTrain < trainigSamples; positiveEndIndexTrain++)
			if (Dtrain[positiveEndIndexTrain] != 1)
				break;

		Stage* root = new Stage(maxBoostingStages, parameters);
		root->train(Xtrain, Dtrain, Xvalidate, Dvalidate, trainigSamples, validationSamples, attribiutesCount,
			extractor, imageList, positiveEndIndexValidate, positiveEndIndexTrain, validationSamples, trainigSamples);

		errfile << "root ready" << endl;

		vector<Stage*> open = { root };
		vector<Stage*> terminals;
		while (open.size() != 0)
		{
			Stage* current = open[0];
			open.erase(open.begin());

			if (current->childs == nullptr)
			{
				terminals.push_back(current);

				errfile << "termninal: " << endl;
			}
			else
			{
				for (int i = 0; i < current->childsCount; i++)
				{
					open.push_back(current->childs[i]);

					errfile << "add child: " << endl;
				}
			}
		}

		errfile << "terminals selected: " << terminals.size() << endl;

		Stage* bestCascade = nullptr;
		Ev = INFINITY;
		for (int i = 0; i < terminals.size(); i++)
		{
			if (terminals[i]->Ev < Ev && terminals[i]->Acurr <= A && terminals[i]->Dcurr >= D)
			{
				bestCascade = terminals[i];
				Ev = terminals[i]->Ev;
			}
		}

		errfile << "best selected" << endl;

		if (bestCascade == nullptr)
		{
			errfile << "0 cascade meet criteria" << endl;
			bestCascade = terminals[0];
		}

		errfile << "Ev " << bestCascade->Ev << endl;
		errfile << "A " << bestCascade->Acurr << endl;
		errfile << "D " << bestCascade->Dcurr << endl;

		Ex = 0.0;
		stagesCount = bestCascade->level;
		errfile << "stages count: " << stagesCount << endl;
		unordered_set<int> usedFeatures;
		double accur = 1;
		for (int i = 0; i < bestCascade->level; i++)
		{
			stages[i] = InitalizeBoostedClassifier(bestCascade->stages[i]);
			thresholds[i] = bestCascade->thresholds[i];
			ais[i] = bestCascade->ais[i];
			dis[i] = bestCascade->dis[i];

			errfile << "ai: " << ais[i] << endl;
			errfile << "di: " << dis[i] << endl;

			auto[fetCount, fet] = bestCascade->stages[i]->getFeatures();
			featurePerStage[i] = new int[fetCount];
			int f2 = 0;
			for (int f = 0; f < fetCount; f++)
			{
				int feat = fet[f];
				if (usedFeatures.count(feat) == 0)
				{
					usedFeatures.insert(feat);
					featurePerStage[i][f2] = feat;
					f2++;
				}
			}
			featuresCounts[i] = f2;

			Ex += f2 * accur;
			accur *= ais[i];

			errfile << "Fet: " << fetCount << endl;
			errfile << "weak classifier cloned" << endl;
		}

		featuresCount = 0;
		for (int s = 0; s < stagesCount; s++)
			featuresCount += featuresCounts[s];

		features = new int[featuresCount];
		int f2 = 0;
		for (int s = 0; s < stagesCount; s++)
		{
			for (int f = 0; f < featuresCounts[s]; f++)
			{
				features[f2] = featurePerStage[s][f];
				f2++;
			}
		}

		Ev = bestCascade->Ev;
		errfile << "Ev: " << Ev << endl;
		errfile << "Ex: " << Ex << endl;
		errfile << "threshold cloned" << endl;

		delete root;

		errfile << "root delted" << endl;

		if (extractor != nullptr)
			delete extractor;

		errfile << "Extractor disposed" << endl;

		std::chrono::system_clock::time_point endGr = std::chrono::system_clock::now();
		errfile << "Time difference = " << std::chrono::duration_cast<std::chrono::nanoseconds>(endGr - beginGr).count() << "ns" << endl;
		errfile << "Time difference = " << std::chrono::duration_cast<std::chrono::milliseconds> (endGr - beginGr).count() << "ms" << endl;

		errfile.close();
	}

	tuple<double, int> calculateOutputForWindowN(Extractor* ext, int wx, int wy, int x, int y, double* features) const override
	{
		//double* features = new double[ext->getFeaturesCount()];

		double out = -1;
		int fet = 0;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			int fc = ext->extractFromWindow(features, featurePerStage[i], featuresCounts[i], wx, wy, x, y);
			out = stages[i]->classify(features, fc, thresholds[i]);
			fet += featuresCounts[i];

			if (out != 1)
			{
				//delete[] features;
				return make_tuple(NEGATIVE_VALUE, fet);
			}
		}
		int fc = ext->extractFromWindow(features, featurePerStage[stagesCount - 1], featuresCounts[stagesCount - 1], wx, wy, x, y);
		out = stages[stagesCount - 1]->calculateOutput(features, fc) - thresholds[stagesCount - 1];
		fet += featuresCounts[stagesCount - 1];

		//delete[] features;
		return make_tuple(out, fet);
	}

	inline double calculateOutputForWindow(Extractor* ext, int wx, int wy, int x, int y, double* features) const override
	{
		//double* features = new double[ext->getFeaturesCount()];

		double out = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			int fc = ext->extractFromWindow(features, featurePerStage[i], featuresCounts[i], wx, wy, x, y);
			out = stages[i]->classify(features, fc, thresholds[i]);

			if (out != 1)
			{
				//delete[] features;
				return NEGATIVE_VALUE;
			}
		}
		int fc = ext->extractFromWindow(features, featurePerStage[stagesCount - 1], featuresCounts[stagesCount - 1], wx, wy, x, y);
		out = stages[stagesCount - 1]->calculateOutput(features, fc) - thresholds[stagesCount - 1];

		//delete[] features;
		return out;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy próbki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attribiutesCount) const override
	{
		int yp = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			yp = stages[i]->classify(X, attribiutesCount, thresholds[i]);

			if (yp != 1)
				return NEGATIVE_VALUE;
		}
		return stages[stagesCount - 1]->calculateOutput(X, attribiutesCount) - thresholds[stagesCount - 1];
	}

	inline tuple<double, int> calculateOutputN(const double* X, int attributesCount) const override
	{
		int features = 0;

		int yp = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			yp = stages[i]->classify(X, attributesCount, thresholds[i]);
			features += featuresCounts[i];

			if (yp != 1)
				return make_tuple(NEGATIVE_VALUE, features);
		}
		features += featuresCounts[stagesCount - 1];
		return  make_tuple(stages[stagesCount - 1]->calculateOutput(X, attributesCount) - thresholds[stagesCount - 1], features);
	}
};
string GraphCascadeOfClassifier::Stage::errfilename = "";


/// <summary>Kaskada klasyfiaktorow --- wersja z grafem i best first search</summary>
class DijkstraGraphCascadeOfClassifier : public Cascade
{
private:
	ClassifierParameters parameters;  /// <summary>Struktura z parametrami dla klasyfiaktorow</summary>
	int maxBoostingStages = 100;

	double* thresholds = nullptr; /// <summary>Progi dla klasyfikatorow</summary>
	Boosting** stages = nullptr; /// <summary>Wybrane w wyniku nauki klasyfiaktory (boostingowe)</summary>
	string boostingType = RealBoost::GetType(); /// <summary>Typ algorytmu boostingowego</summary>

	double* ais = nullptr;
	double* dis = nullptr;

public:
	using Classifier::train;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::saveModel;
	using Classifier::saveModelOld;
	using Classifier::loadModel;

	~DijkstraGraphCascadeOfClassifier()
	{
		for (int i = 0; i < stagesCount; i++)
			delete stages[i];
		delete[] stages;
		delete[] thresholds;
		delete[] ais;
		delete[] dis;
	}

	/// <summary>Utworzenie  kaskady klasyfikatorow na podstawie domyslnych parametrow</summary>
	DijkstraGraphCascadeOfClassifier()
	{
		this->boostingType = parameters.boostingType;
		this->maxBoostingStages = parameters.boostingStages;
		this->K = parameters.cascadeStages;
		this->A = parameters.maxFAR;
		this->D = parameters.minSpecificity;
		learningMethod = "VJ";

		ais = new double[K];
		dis = new double[K];
		thresholds = new double[K];
		stages = new Boosting *[K];
		featurePerStage = new int*[K];
		featuresCounts = new int[K];

		ForceSeeds = parameters.forceSeeds;
		ValidSeed1 = parameters.validSeed1;
		ValidSeed2 = parameters.validSeed2;
		TrainSeed1 = parameters.trainSeed1;
		TrainSeed2 = parameters.trainSeed2;

		ResizeSetsWhenResampling = parameters.resizeSetsWhenResampling;
		ResamplingMaxValSize[0] = parameters.resamplingMaxValSize;
		ResamplingMaxTrainSize[0] = parameters.resamplingMaxTrainSize1;
		ResamplingMaxTrainSize[1] = parameters.resamplingMaxTrainSize2;
	}

	/// <summary>Utworzenie kaskady klasyfikatorow na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	DijkstraGraphCascadeOfClassifier(const ClassifierParameters& parameters)
	{
		this->parameters = parameters;

		this->boostingType = this->parameters.boostingType;
		this->maxBoostingStages = this->parameters.boostingStages;
		this->K = this->parameters.cascadeStages;
		this->A = this->parameters.maxFAR;
		this->D = this->parameters.minSpecificity;
		this->learningMethod = this->parameters.learningMethod;

		ais = new double[K];
		dis = new double[K];
		thresholds = new double[K];
		stages = new Boosting *[K];
		featurePerStage = new int*[K];
		featuresCounts = new int[K];

		ForceSeeds = this->parameters.forceSeeds;
		ValidSeed1 = this->parameters.validSeed1;
		ValidSeed2 = this->parameters.validSeed2;
		TrainSeed1 = this->parameters.trainSeed1;
		TrainSeed2 = this->parameters.trainSeed2;

		ResizeSetsWhenResampling = this->parameters.resizeSetsWhenResampling;
		ResamplingMaxValSize[0] = this->parameters.resamplingMaxValSize;
		ResamplingMaxTrainSize[0] = this->parameters.resamplingMaxTrainSize1;
		ResamplingMaxTrainSize[1] = this->parameters.resamplingMaxTrainSize2;
	}

	/// <summary>Zaladowanie kaskady klasyfikatorow z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	DijkstraGraphCascadeOfClassifier(string path) { loadModel(path); }

	/// <summary>Zaladowanie kaskady klasyfikatorow z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	DijkstraGraphCascadeOfClassifier(ifstream& input) { loadModel(input); }

	/// <summary>Zaladowanie kaskady klasyfikatorow z podanego strumienia oraz zapisanie parametrow w odpowiedniej strukturze</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'params'>Struktura z parametrami dla klasyfiaktora</param>
	DijkstraGraphCascadeOfClassifier(ifstream& input, ClassifierParameters& params) { loadModel(input, params); }

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "ClassifierCascade";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca instacje boostingowego klasyfikatora</summary>
	/// <returns>Instacja boostingowego klasyfikatora</returns>
	Boosting* InitalizeBoostedClassifier()
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost(parameters);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost(parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	Boosting* InitalizeBoostedClassifier(Boosting* toCopy)
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost((AdaBoost*)toCopy);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost((RealBoost*)toCopy);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	Boosting* InitalizeBoostedClassifier(ifstream& input)
	{
		if (boostingType == AdaBoost::GetType())
			return new AdaBoost(input, parameters);
		else if (boostingType == RealBoost::GetType())
			return new RealBoost(input, parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <param name = 'full'>Pe?ny/Skrócony opis klasyfikatora</param>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";

		text += "Used boosting type: " + boostingType + "\r\n";
		text += "Learning method: " + learningMethod + "\r\n";
		text += "Stages count: " + to_string(stagesCount) + "\r\n";
		text += "Max FAR: " + to_string(A) + "\r\n";
		text += "Min sensitivity: " + to_string(D) + "\r\n";

		for (int i = 0; i < stagesCount; i++)
		{
			text += "Stage " + to_string(i) + ":\r\n";
			text += "Threshold " + to_string(thresholds[i]) + ":\r\n";
			text += "Boosting stages: " + to_string(stages[i]->getStagesNumber()) + "\r\n";
		}

		text += "\r\n";
		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		if (thresholds != nullptr)
		{
			for (int i = 0; i < stagesCount; i++)
				delete stages[i];
			delete[] stages;
			delete[] thresholds;
			for (int i = 0; i < stagesCount; i++)
				delete[] featurePerStage[i];
			delete[] featurePerStage;
			delete[] featuresCounts;
			delete[] features;
			delete[] ais;
			delete[] dis;
		}

		string fieldName, type;
		skipHeader(input);
		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> stagesCount;
			input >> fieldName >> boostingType;
			input >> fieldName >> maxBoostingStages;
			input >> fieldName >> Ev;
			input >> fieldName >> Ex;
			input >> fieldName >> A;
			input >> fieldName >> D;
			input >> fieldName >> K;
			input >> fieldName >> featuresCount;

			features = new int[featuresCount];
			featuresCounts = new int[K];
			featurePerStage = new int*[K];
			ais = new double[K];
			dis = new double[K];

			skipHeader(input);
			int k = 0;
			for (int i = 0; i < stagesCount; i++)
			{
				input >> fieldName >> featuresCounts[i];
				featurePerStage[i] = new int[featuresCounts[i]];
				for (int j = 0; j < featuresCounts[i]; j++)
				{
					input >> featurePerStage[i][j];
					features[k] = featurePerStage[i][j];
					k++;
				}
			}
			for (int i = stagesCount; i < K; i++)
				featurePerStage[i] = nullptr;

			thresholds = new double[K];
			stages = new Boosting *[K];
			skipHeader(input);
			for (int i = 0; i < stagesCount; i++)
			{
				skipHeader(input);
				input >> fieldName >> thresholds[i];
				stages[i] = InitalizeBoostedClassifier(input);
			}
			for (int i = stagesCount; i < K; i++)
				stages[i] = nullptr;

			skipHeader(input);
			input >> fieldName >> learningMethod;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;

		parameters.cascadeStages = K;
		parameters.maxFAR = A;
		parameters.minSpecificity = D;
		parameters.boostingStages = maxBoostingStages;
		strncpy_s(parameters.boostingType, ClassifierParameters::STRING_BUFFER, boostingType.c_str(), _TRUNCATE);
		strncpy_s(parameters.learningMethod, ClassifierParameters::STRING_BUFFER, learningMethod.c_str(), _TRUNCATE);
	}


	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'param'>Struktura z parametrami dla klasyfikatora</param>
	void loadModel(ifstream& input, ClassifierParameters& params) override
	{
		loadModel(input);

		// TODO Get weak classifier and boosting parameters

		params.cascadeStages = K;
		params.maxFAR = A;
		params.minSpecificity = D;
		params.boostingStages = maxBoostingStages;
		strncpy_s(params.boostingType, ClassifierParameters::STRING_BUFFER, boostingType.c_str(), _TRUNCATE);
		strncpy_s(params.learningMethod, ClassifierParameters::STRING_BUFFER, learningMethod.c_str(), _TRUNCATE);
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Stages: " << stagesCount << endl;
		output << "Boosting_Type: " << boostingType << endl;
		output << "Boosting_Stages: " << maxBoostingStages << endl;
		output << "Ex(s): " << Ev << endl;
		output << "Ex(f): " << Ex << endl;
		output << "A: " << A << endl;
		output << "D: " << D << endl;
		output << "K: " << K << endl;
		output << "Total_features: " << featuresCount << endl;
		createSecondaryHeader(output, "Features:");
		for (int i = 0; i < stagesCount; i++)
		{
			output << "Features_Count: " << featuresCounts[i] << endl;
			for (int j = 0; j < featuresCounts[i]; j++)
				output << featurePerStage[i][j] << " ";
			output << endl;
		}
		createSecondaryHeader(output, "Boosted_Classifiers:");
		for (int i = 0; i < stagesCount; i++)
		{
			createSecondaryHeader(output, "Boosted_Classifier_" + to_string(i));
			output << "Threshold: " << thresholds[i] << endl;
			stages[i]->saveModel(output);
		}
		createSecondaryHeader(output, "Additional_Info:");
		output << "Training_method: " << learningMethod << endl;
		output << "Use_graphs: " << true << endl;
		output << "Is_uniform: " << parameters.isUniform << endl;
		output << "Childs: " << parameters.childsCount << endl;
		output << "Splits: " << parameters.splits << endl;
		output << "Use_dijkstra: " << true << endl;
		createSecondaryHeader(output, "Resampling_Settings:");
		output << "Extractor: " << parameters.extractorType << endl;
		if (parameters.extractorType == HaarExtractor::GetType())
		{
			output << "Templates: " << parameters.t << endl;
			output << "Scales: " << parameters.s << endl;
			output << "Positions: " << parameters.ps << endl;
		}
		else if (parameters.extractorType == HOGExtractor::GetType())
		{
			output << "Bins: " << parameters.b << endl;
			output << "Blocks (X): " << parameters.nx << endl;
			output << "Blocks (Y): " << parameters.ny << endl;
		}
		else
		{
			output << "Harmonic: " << parameters.p << endl;
			output << "Degree: " << parameters.q << endl;
			output << "Rings: " << parameters.r << endl;
			output << "Rings_type: " << parameters.rt << endl;
			output << "Width: " << parameters.d << endl;
			output << "Overlap: " << parameters.w << endl;
		}
		output << "Resampling_scales: " << parameters.resScales << endl;
		output << "Scaling_factor: " << parameters.scaleFactor << endl;
		output << "Min_window_size: " << parameters.minWindow << endl;
		output << "Jumping_factor: " << parameters.jumpingFactor << endl;
		output << "Repetition_per_image: " << parameters.repetitionPerImage << endl;
		if (ais != nullptr)
		{
			createSecondaryHeader(output, "Validation_Scores:");
			output << "ai: ";
			for (int i = 0; i < stagesCount; i++)
				output << ais[i] << " ";
			output << endl;
			output << "di: ";
			for (int i = 0; i < stagesCount; i++)
				output << dis[i] << " ";
			output << endl;
		}
	}

	void saveModelOld(ofstream& output) const override
	{
		output << "ClassifierCascadeVJ_v2" << endl;
		output << Ev << endl;
		output << A << endl;
		output << 0 << endl;
		output << D << endl;
		output << 0 << endl;
		output << K << endl;
		output << stagesCount << endl;
		output << boostingType << endl;
		for (int i = 0; i < (int)stagesCount; i++)
		{
			output << thresholds[i] << endl;
			stages[i]->saveModelOld(output);
		}
	}

	class Stage
	{
	public:
		string id = "";

		double* thresholds = nullptr; /// <summary>Progi dla klasyfikatorow</summary>
		Boosting** stages = nullptr; /// <summary>Wybrane w wyniku nauki klasyfiaktory (boostingowe)</summary>


		double* ais = nullptr;
		double* dis = nullptr;

		int level = 0;
		int childsCount = 3; // wieksze rowne 1 po przekroczeniu lastSplitLevel bedzie zawsze rowne 1, musi być nieparzyste
		int splits = 1;

		double Ai = 1;
		double Di = 0;
		double Ex;

		Stage* parent = nullptr;

		const double* const* Xtrain;
		const int* Dtrain;
		const double* const* Xvalidate;
		const int* Dvalidate;
		int trainigSamples;
		int validationSamples;
		int canDeleteAfterVal;
		int canDeleteAfterTrain;
		int positiveEndIndexValidate;
		int positiveEndIndexTrain;

		long seedVal, seedTrain, seedVStep, seedTStep;

		bool isResampled = false;
		bool samplesDeleted = false;
		int childsActive;

		Stage(int childs, int currlevel)
		{
			Ex = 0;

			level = currlevel;
			childsCount = childs;
			childsActive = childsCount;

			stages = new Boosting *[level];
			stages[level - 1] = nullptr;
			thresholds = new double[level];
			ais = new double[level];
			dis = new double[level];
		}

		~Stage()
		{
			if (level > 0)
			{
				delete[] ais;
				delete[] dis;
				delete[] thresholds;

				delete stages[level - 1];
				delete[] stages;
			}
		}

		/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
		/// <param name = 'X'>Cechy prÃ³bki do klasyfikacji</param>
		/// <returns>Odpowiedz klasyfikatora</returns>
		inline int classify(const double* X, int attribiutesCount) const
		{
			int yp = -1;

			for (int i = 0; i < level; i++)
			{
				yp = stages[i]->classify(X, attribiutesCount, thresholds[i]);

				if (yp != 1)
					return yp;
			}
			return yp;
		}
	};

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Cechy próbek ucz?cych</param>
	/// <param name = 'D'>Klasy próbek ucz?cych</param>
	/// <param name = 'Indices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	void train(const double* const* XtrainIn, const int* DtrainIn, const double* const* XvalidateIn, const int* DvalidateIn, int trainigSamplesIn, int validationSamplesIn, int attribiutesCount) override
	{
	}

	tuple<double, int> calculateOutputForWindowN(Extractor* ext, int wx, int wy, int x, int y, double* features) const override
	{
		//double* features = new double[ext->getFeaturesCount()];

		double out = -1;
		int fet = 0;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			int fc = ext->extractFromWindow(features, featurePerStage[i], featuresCounts[i], wx, wy, x, y);
			out = stages[i]->classify(features, fc, thresholds[i]);
			fet += featuresCounts[i];

			if (out != 1)
			{
				//delete[] features;
				return make_tuple(NEGATIVE_VALUE, fet);
			}
		}
		int fc = ext->extractFromWindow(features, featurePerStage[stagesCount - 1], featuresCounts[stagesCount - 1], wx, wy, x, y);
		out = stages[stagesCount - 1]->calculateOutput(features, fc) - thresholds[stagesCount - 1];
		fet += featuresCounts[stagesCount - 1];

		//delete[] features;
		return make_tuple(out, fet);
	}

	inline double calculateOutputForWindow(Extractor* ext, int wx, int wy, int x, int y, double* features) const override
	{
		//double* features = new double[ext->getFeaturesCount()];

		double out = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			int fc = ext->extractFromWindow(features, featurePerStage[i], featuresCounts[i], wx, wy, x, y);
			out = stages[i]->classify(features, fc, thresholds[i]);

			if (out != 1)
			{
				//delete[] features;
				return NEGATIVE_VALUE;
			}
		}
		int fc = ext->extractFromWindow(features, featurePerStage[stagesCount - 1], featuresCounts[stagesCount - 1], wx, wy, x, y);
		out = stages[stagesCount - 1]->calculateOutput(features, fc) - thresholds[stagesCount - 1];

		//delete[] features;
		return out;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy próbki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attribiutesCount) const override
	{
		int yp = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			yp = stages[i]->classify(X, attribiutesCount, thresholds[i]);

			if (yp != 1)
				return NEGATIVE_VALUE;
		}
		return stages[stagesCount - 1]->calculateOutput(X, attribiutesCount) - thresholds[stagesCount - 1];
	}

	inline tuple<double, int> calculateOutputN(const double* X, int attributesCount) const override
	{
		int features = 0;

		int yp = -1;
		for (int i = 0; i < stagesCount - 1; i++)
		{
			yp = stages[i]->classify(X, attributesCount, thresholds[i]);
			features += featuresCounts[i];

			if (yp != 1)
				return make_tuple(NEGATIVE_VALUE, features);
		}
		features += featuresCounts[stagesCount - 1];
		return  make_tuple(stages[stagesCount - 1]->calculateOutput(X, attributesCount) - thresholds[stagesCount - 1], features);
	}
};