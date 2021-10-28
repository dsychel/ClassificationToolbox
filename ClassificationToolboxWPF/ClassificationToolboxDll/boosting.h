#pragma once
//#include<vector>
//#include<iostream>
//#include<fstream>
//#include<map>
//#include<string>
//#include<omp.h>
//#include<list>
//#include<unordered_set>

#include"utills.h"
#include"configuration.h"
#include"classifier.h"

using namespace std;

/// <summary>Klasa bazowa dla klasyfikatorow boostingowych</summary>
class Boosting : public Classifier
{
protected:
	int T; /// <summary>Maksymalna liczba etapow w boostingu</summary>
	int stages = 0;
	bool useTrimming; /// <summary>Czy wlaczyc wygaszanie wag?</summary>
	double trimminingThreshold; /// <summary>Prog do wygaszania wag</summary>
	double trimmingMinSamples = 0.01; /// <summary>Minimalny procent probek, ktory musi uczestniczyc w nauce</summary>
	BoostableClassifier** ht = nullptr;

public:
	using Classifier::train;

	virtual ~Boosting()
	{
		if (ht != nullptr)
		{
			for (int i = 0; i < stages; i++)
				delete ht[i];
			delete[] ht;
		}
	}

	/// <summary>Zwraca liczbe etapow</summary>
	/// <returns>Liczba etapow boostingu</returns>
	virtual int getStagesNumber() { return stages; }

	/// <summary>Dodaje nowy etap do silnego klasyfikaotra</summary>
	/// <param name = 'X'>Cechy próbek ucz¹cych</param>
	/// <param name = 'D'>Klasy próbek ucz¹cych</param>
	/// <param name = 'weights'>Aktualne wagi probek</param>
	/// <param name = 'indices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	virtual void addStage(const double* const* X, const int* D, const int* indices, double* &weights, int samplesCount, int attributesCount, int indicesCount) = 0;

	/// <summary>Dodaje nowy etap do silnego klasyfikaotra</summary>
	/// <param name = 'X'>Cechy próbek ucz¹cych</param>
	/// <param name = 'D'>Klasy próbek ucz¹cych</param>
	/// <param name = 'weights'>Aktualne wagi probek</param>
	void addStage(const double* const* X, const int* D, double* &weights, int samplesCount, int attributesCount)
	{
		int *indices = new int[samplesCount];
		for (int i = 0; i < samplesCount; i++)
			indices[i] = i;

		addStage(X, D, indices, weights, samplesCount, attributesCount, samplesCount);

		delete[] indices;
	}

	virtual void removeStage() = 0;

	virtual void endStagewiseTraining(const int* indices, int attributesCount, int indicesCount) = 0;

	virtual void initializeData(const double* const* X, const int* indices, int samplesCount, int attributesCount, int indicesCount) = 0;

	virtual void clearData(const int* indices, int attributesCount, int indicesCount) = 0;
};

/// <summary>Klasyfiaktor RealBoost</summary>
class RealBoost : public Boosting
{
private:
	ClassifierParameters parameters;  /// <summary>Struktura z parametrami dla klasyfiaktorow</summary>
	string classifierType; /// <summary>Typ s³abego klasyfikatora</summary>

	int** sortedIndices = nullptr;
	double** minmax = nullptr;
	int** xInBin = nullptr;

	bool isStump = false;
	bool useBins = false;
public:
	using Classifier::train;
	using Boosting::addStage;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::loadModel;
	using Classifier::saveModel;

	~RealBoost() override
	{
	}

	/// <summary>Utworzenie RealBoosta na podstawie domyslnych parametrow</summary>
	RealBoost()
	{
		this->classifierType = parameters.classifierType;
		this->T = parameters.boostingStages;
		this->useTrimming = parameters.useWeightTrimming;
		this->trimminingThreshold = parameters.weightTrimmingThreshold;
		this->trimmingMinSamples = parameters.weightTrimmingMinSamples;

		this->isStump = classifierType == DecisionStump::GetType();
		this->useBins = classifierType == RegularBins::GetType() || classifierType == BinnedDecisionStump::GetType() || classifierType == BinnedTree::GetType();
	}

	/// <summary>Utworzenie RealBoosta na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	RealBoost(const ClassifierParameters &parameters)
	{
		this->parameters = parameters;
		this->classifierType = parameters.classifierType;
		this->T = parameters.boostingStages;
		this->useTrimming = parameters.useWeightTrimming;
		this->trimminingThreshold = parameters.weightTrimmingThreshold;
		this->trimmingMinSamples = parameters.weightTrimmingMinSamples;

		this->isStump = classifierType == DecisionStump::GetType();
		this->useBins = classifierType == RegularBins::GetType() || classifierType == BinnedDecisionStump::GetType() || classifierType == BinnedTree::GetType();
	}


	/// <summary>Utworzenie RealBoosta na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	RealBoost(const RealBoost *toCopy)
	{
		this->parameters = toCopy->parameters;
		this->classifierType = toCopy->classifierType;
		this->T = toCopy->T;
		this->stages = toCopy->stages;
		this->useTrimming = toCopy->useTrimming;
		this->trimminingThreshold = toCopy->trimminingThreshold;
		this->trimmingMinSamples = toCopy->trimmingMinSamples;

		this->isStump = toCopy->isStump;
		this->useBins = toCopy->useBins;

		this->featuresCount = toCopy->featuresCount;
		this->features = new int[featuresCount];
		for (int i = 0; i < featuresCount; i++)
			this->features[i] = toCopy->features[i];

		if (stages > 0)
		{
			this->ht = new BoostableClassifier*[T];
			for (int i = 0; i < stages; i++)
				ht[i] = (InitalizeWeakClassifier(toCopy->ht[i]));
		}
	}

	/// <summary>Zaladowanie LogitBoosta z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	RealBoost(string path) { loadModel(path); }

	/// <summary>Zaladowanie LogitBoosta z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	RealBoost(ifstream &input) { loadModel(input); }

	/// <summary>Zaladowanie DecisionStump-a  z podanego strumienia oraz zapisanie parametrow w odpowiedniej strukturze</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'params'>Struktura z parametrami dla klasyfiaktora</param>
	RealBoost(ifstream &input, ClassifierParameters &params) { loadModel(input, params); }

	/// <summary>Zwraca instacje slabego klasyfikatora</summary>
	/// <returns>Instacja s³abego  klasyfikatora</returns>
	BoostableClassifier* InitalizeWeakClassifier()
	{
		if (classifierType == WeakPerceptron::GetType())
			return new WeakPerceptron(parameters);
		else if (classifierType == DecisionStump::GetType())
			return new DecisionStump(parameters);
		else if (classifierType == BinnedDecisionStump::GetType())
			return new BinnedDecisionStump(parameters);
		else if (classifierType == RegularBins::GetType())
			return new RegularBins(parameters);
		else if (classifierType == BinnedTree::GetType())
			return new BinnedTree(parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	BoostableClassifier* InitalizeWeakClassifier(BoostableClassifier* toCopy)
	{
		if (classifierType == WeakPerceptron::GetType())
			return new WeakPerceptron((WeakPerceptron*)toCopy);
		else if (classifierType == DecisionStump::GetType())
			return new DecisionStump((DecisionStump*)toCopy);
		else if (classifierType == BinnedDecisionStump::GetType())
			return new BinnedDecisionStump((BinnedDecisionStump*)toCopy);
		else if (classifierType == RegularBins::GetType())
			return new RegularBins((RegularBins*)toCopy);
		else if (classifierType == BinnedTree::GetType())
			return new BinnedTree((BinnedTree*)toCopy);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	/// <summary>Zwraca instacje slabego klasyfikatora utworzonego na podstawie danych z pliku</summary>
	/// <param name = 'input'>Strumien do pliku, w ktortm zostal zapisany slaby klasyfikator</param>
	/// <returns>Instacja s³abego  klasyfikatora</returns>
	BoostableClassifier* InitalizeWeakClassifier(ifstream &input)
	{
		if (classifierType == WeakPerceptron::GetType())
			return new WeakPerceptron(input, parameters);
		else if (classifierType == DecisionStump::GetType())
			return new DecisionStump(input, parameters);
		else if (classifierType == BinnedDecisionStump::GetType())
			return new BinnedDecisionStump(input, parameters);
		else if (classifierType == RegularBins::GetType())
			return new RegularBins(input, parameters);
		else if (classifierType == BinnedTree::GetType())
			return new BinnedTree(input, parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	void initializeData(const double* const* X, const int* indices, int samplesCount, int attributesCount, int indicesCount) override
	{
		if (!useTrimming && isStump && sortedIndices == nullptr)
		{
			sortedIndices = new int*[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int i = 0; i < attributesCount; i++)
			{
				sortedIndices[i] = new int[indicesCount];
				memcpy(sortedIndices[i], indices, indicesCount * sizeof(int));
				sort_indexes(X, sortedIndices[i], indicesCount, i);
			}
		}
		if (useBins && xInBin == nullptr)
		{
			minmax = new double*[attributesCount];
			for (int i = 0; i < attributesCount; i++)
				minmax[i] = new double[2];

			xInBin = new int*[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				xInBin[indices[i]] = new int[attributesCount];

			calculateRanges(X, minmax, indices, indicesCount, attributesCount, parameters.outlayerPercent);
			assignBins(X, minmax, xInBin, indices, indicesCount, attributesCount, parameters.treeBins);
		}
	}

	void clearData(const int* indices, int attributesCount, int indicesCount) override
	{
		if (sortedIndices != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] sortedIndices[i];
			delete[] sortedIndices;
			sortedIndices = nullptr;
		}
		if (xInBin != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] minmax[i];
			delete[] minmax;
			minmax = nullptr;

			for (int i = 0; i < indicesCount; i++)
				delete[] xInBin[indices[i]];
			delete[] xInBin;
			xInBin = nullptr;
		}
	}

	void endStagewiseTraining(const int* indices, int attributesCount, int indicesCount) override
	{
		if (sortedIndices != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] sortedIndices[i];
			delete[] sortedIndices;
			sortedIndices = nullptr;
		}
		if (xInBin != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] minmax[i];
			delete[] minmax;
			minmax = nullptr;

			for (int i = 0; i < indicesCount; i++)
				delete[] xInBin[indices[i]];
			delete[] xInBin;
			xInBin = nullptr;
		}

		if (features != nullptr)
			delete[] features;

		int totalFeatures = stages;
		if (classifierType == BinnedTree::GetType())
			totalFeatures *= (int)(pow(2, parameters.maxTreeLevel) - 1);
		features = new int[totalFeatures];

		unordered_set<int> uniqueFeat;
		int f2 = 0;
		for (int i = 0; i < stages; i++)
		{
			auto[fetNum, feat] = ht[i]->getFeatures();
			for (int j = 0; j < fetNum; j++)
			{
				int feature = feat[j];
				if (uniqueFeat.count(feature) == 0)
				{
					features[f2] = feature;
					uniqueFeat.insert(feature);
					f2++;

				}
			}
		}
		featuresCount = f2;
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "RealBoost";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <param name = 'full'>Pe³ny/Skrócony opis klasyfikatora</param>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = "";
		text += getType() + "\r\n";
		text += classifierType + "\r\n";
		text += "Use Weight Trimming:" + to_string(useTrimming) + "\r\n";
		text += "Trimmining Threshold:" + to_string(trimminingThreshold) + "\r\n";
		text += "Trimming Minimum Samples:" + to_string(trimmingMinSamples) + "\r\n";
		text += "Stages:" + to_string(stages) + "\r\n";
		for (int i = 0; i < stages; i++)
		{
			text += "------\r\n";
			text += to_string(i) + ": \r\n";
			text += "WeakClassifier:\r\n";
			text += ht[i]->toString() + "\r\n";
		}
		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream &input) override
	{
		if (ht != nullptr)
		{
			for (int i = 0; i < stages; i++)
				delete ht[i];
			delete[] ht;
		}
		if (features != nullptr)
			delete[] features;

		string fieldName, type;
		skipHeader(input);
		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> stages;
			input >> fieldName >> classifierType;
			input >> fieldName >> featuresCount;

			ht = new BoostableClassifier*[stages];
			features = new int[featuresCount];
			for (int i = 0; i < featuresCount; i++)
				input >> features[i];

			skipHeader(input);
			input >> fieldName >> useTrimming;
			if (useTrimming)
			{
				input >> fieldName >> trimminingThreshold;
				input >> fieldName >> trimmingMinSamples;
			}

			skipHeader(input);
			for (int i = 0; i < stages; i++)
			{
				skipHeader(input);
				ht[i] = InitalizeWeakClassifier(input);
			}
			T = stages;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;

		parameters.boostingStages = T;
		strncpy_s(parameters.boostingType, ClassifierParameters::STRING_BUFFER, type.c_str(), _TRUNCATE);
		strncpy_s(parameters.classifierType, ClassifierParameters::STRING_BUFFER, classifierType.c_str(), _TRUNCATE);
		parameters.useWeightTrimming = useTrimming;
		parameters.weightTrimmingThreshold = trimminingThreshold;
		parameters.weightTrimmingMinSamples = trimmingMinSamples;

		this->isStump = classifierType == DecisionStump::GetType();
		this->useBins = classifierType == RegularBins::GetType() || classifierType == BinnedDecisionStump::GetType() || classifierType == BinnedTree::GetType();
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'param'>Struktura z parametrami dla boostingu</param>
	void loadModel(ifstream &input, ClassifierParameters &params) override
	{
		loadModel(input);

		// TODO parametry slabych klasyfikatorow

		params.boostingStages = T;
		strncpy_s(parameters.boostingType, ClassifierParameters::STRING_BUFFER, getType().c_str(), _TRUNCATE);
		strncpy_s(parameters.classifierType, ClassifierParameters::STRING_BUFFER, classifierType.c_str(), _TRUNCATE);
		params.useWeightTrimming = useTrimming;
		params.weightTrimmingThreshold = trimminingThreshold;
		params.weightTrimmingMinSamples = trimmingMinSamples;
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream &output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Stages: " << stages << endl;
		output << "Weak_Classifier_Type: " << classifierType << endl;
		output << "Features_Count: " << featuresCount << endl;
		for (int i = 0; i < featuresCount; i++)
			output << features[i] << " ";
		output << endl;
		createSecondaryHeader(output, "Training_Parameters:");
		output << "Using_Weight_Trimming: " << useTrimming << endl;
		if (useTrimming)
		{
			output << "Trimming_Threshold: " << trimminingThreshold << endl;
			output << "Trimming_Minimum_Samples: " << trimmingMinSamples << endl;
		}
		createSecondaryHeader(output, "Weak_Classifiers:");
		for (int i = 0; i < stages; i++)
		{
			createSecondaryHeader(output, "Weak_Classifier_" + to_string(i));
			ht[i]->saveModel(output);
		}
	}

	void saveModelOld(ofstream &output) const override
	{
		output << getType() << endl;
		output << useTrimming << endl;
		output << trimminingThreshold << endl;
		output << trimmingMinSamples << endl;
		output << stages << endl;
		output << classifierType << endl;
		for (int i = 0; i < stages; i++)
		{
			ht[i]->saveModelOld(output);
		}
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'indices'>Indeksy probek bioracych udzial w uczeniu</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const double* const* X, const int* D, const int* indices, int samplesCount, int attributesCount, int indicesCount) override
	{
		bool sortedData = false;
		bool assignedBins = false;

		if (ht != nullptr)
		{
			for (int i = 0; i < stages; i++)
				delete ht[i];
			delete[] ht;
		}
		if (features != nullptr)
			delete[] features;
		stages = T;
		ht = new BoostableClassifier*[T];

#pragma region Clasifier Parameters Initialization
		int* trimingIndices = new int[indicesCount];
		memcpy(trimingIndices, indices, indicesCount * sizeof(int));
		int indicesToUse = indicesCount;

		if (!useTrimming && isStump && sortedIndices == nullptr)
		{
			sortedData = true;
			sortedIndices = new int*[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int i = 0; i < attributesCount; i++)
			{
				sortedIndices[i] = new int[indicesCount];
				memcpy(sortedIndices[i], indices, indicesCount * sizeof(int));
				sort_indexes(X, sortedIndices[i], indicesCount, i);
			}
		}
		if (useBins && xInBin == nullptr)
		{
			assignedBins = true;
			minmax = new double*[attributesCount];
			for (int i = 0; i < attributesCount; i++)
				minmax[i] = new double[2];

			xInBin = new int*[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				xInBin[indices[i]] = new int[attributesCount];

			calculateRanges(X, minmax, indices, indicesCount, attributesCount, parameters.outlayerPercent);
			assignBins(X, minmax, xInBin, indices, indicesCount, attributesCount, parameters.treeBins);
		}
#pragma endregion

		double* w = new double[samplesCount];
		for (int i = 0; i < indicesCount; i++)
			w[indices[i]] = (1.0 / indicesCount);

		unordered_set<int> fet;
		// Kolejne etapy boostingu
		for (int i = 0; i < T; i++)
		{
			BoostableClassifier *h = InitalizeWeakClassifier();
			if (!useTrimming && isStump)
				((DecisionStump *)h)->train(X, D, sortedIndices, w, samplesCount, attributesCount, indicesCount);
			else if (useBins)
				((BoostableBinnedClassifier *)h)->train(xInBin, minmax, D, trimingIndices, w, samplesCount, attributesCount, indicesToUse);
			else
				h->train(X, D, trimingIndices, w, samplesCount, attributesCount, indicesToUse);

			double sumW = 0.0;
			for (int j = 0; j < indicesCount; j++)
			{
				double output = h->calculateOutput(X[indices[j]], attributesCount);

				w[indices[j]] = w[indices[j]] * exp(-(D[indices[j]] * output));
				sumW += w[indices[j]];
			}
			for (int j = 0; j < indicesCount; j++)
				w[indices[j]] = w[indices[j]] / sumW;

			if (useTrimming)
			{
				memcpy(trimingIndices, indices, indicesCount * sizeof(int));
				sort_indexes_dsc(w, trimingIndices, indicesCount);

				double sum = 0;
				unsigned int z = 0;
				for (; z < (unsigned int)indicesCount; z++)
				{
					sum += w[trimingIndices[z]];
					if (sum >= trimminingThreshold && z >= (unsigned int)(trimmingMinSamples * indicesCount))
						break;
				}
				indicesToUse = z;
			}

			auto[fetNum, feat] = h->getFeatures();
			for (int i = 0; i < fetNum; i++)
				fet.insert(feat[i]);

			ht[i] = h;
		}
		featuresCount = (int)fet.size();
		features = new int[featuresCount];
		int i = 0;
		for (auto feature : fet)
		{
			features[i] = feature;
			i++;
		}

		if (sortedData)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] sortedIndices[i];
			delete[] sortedIndices;
			sortedIndices = nullptr;
		}
		if (assignedBins)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] minmax[i];
			delete[] minmax;
			minmax = nullptr;

			for (int i = 0; i < indicesCount; i++)
				delete[] xInBin[indices[i]];
			delete[] xInBin;
			xInBin = nullptr;
		}
		delete[] w;
		delete[] trimingIndices;
	}

	/// <summary>Dodaje nowy etap do silnego klasyfikaotra</summary>
	/// <param name = 'X'>Cechy próbek ucz¹cych</param>
	/// <param name = 'D'>Klasy próbek ucz¹cych</param>
	/// <param name = 'weights'>Aktualne wagi probek</param>
	/// <param name = 'indices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	void addStage(const double* const* X, const int* D, const int* indices, double* &weights, int samplesCount, int attributesCount, int indicesCount) override
	{
		if (stages >= T)
		{
			BoostableClassifier** ht_tmp = ht;
			ht = new BoostableClassifier*[T + 1];
			memcpy(ht, ht_tmp, T * sizeof(BoostableClassifier *));

			T++;
			delete[] ht_tmp;
		}

		if (!useTrimming && isStump && sortedIndices == nullptr)
		{
			sortedIndices = new int*[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int i = 0; i < attributesCount; i++)
			{
				sortedIndices[i] = new int[indicesCount];
				memcpy(sortedIndices[i], indices, indicesCount * sizeof(int));
				sort_indexes(X, sortedIndices[i], indicesCount, i);
			}
		}
		if (useBins && xInBin == nullptr)
		{
			minmax = new double*[attributesCount];
			for (int i = 0; i < attributesCount; i++)
				minmax[i] = new double[2];

			xInBin = new int*[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				xInBin[indices[i]] = new int[attributesCount];

			calculateRanges(X, minmax, indices, indicesCount, attributesCount, parameters.outlayerPercent);
			assignBins(X, minmax, xInBin, indices, indicesCount, attributesCount, parameters.treeBins);
		}

		int* trimingIndices = nullptr;
		int indicesToUse = indicesCount;

		if (useTrimming)
		{
			trimingIndices = new int[indicesCount];
			memcpy(trimingIndices, indices, indicesCount * sizeof(int));
		}

		if (weights == nullptr)
		{
			stages = 0;

			if (ht != nullptr)
			{
				for (int i = 0; i < T; i++)
					delete ht[i];
				delete[] ht;
			}

			ht = new BoostableClassifier*[T];

			weights = new double[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				weights[indices[i]] = (1.0 / indicesCount);
		}
		else if (useTrimming)
		{
			sort_indexes_dsc(weights, trimingIndices, indicesCount);

			double sum = 0;
			unsigned int z = 0;
			for (; z < (unsigned int)indicesCount; z++)
			{
				sum += weights[trimingIndices[z]];
				if (sum >= trimminingThreshold && z >= (unsigned int)(trimmingMinSamples * indicesCount))
					break;
			}
			indicesToUse = z;
		}

		BoostableClassifier *h = InitalizeWeakClassifier();
		if (!useTrimming)
		{
			if (sortedIndices != nullptr && isStump)
				((DecisionStump *)h)->train(X, D, sortedIndices, weights, samplesCount, attributesCount, indicesCount);
			else if (xInBin != nullptr && useBins)
				((BoostableBinnedClassifier *)h)->train(xInBin, minmax, D, indices, weights, samplesCount, attributesCount, indicesToUse);
			else
				h->train(X, D, indices, weights, samplesCount, attributesCount, indicesToUse);
		}
		if (useTrimming)
		{
			if (xInBin != nullptr && useBins)
				((BoostableBinnedClassifier *)h)->train(xInBin, minmax, D, trimingIndices, weights, samplesCount, attributesCount, indicesToUse);
			else
				h->train(X, D, trimingIndices, weights, samplesCount, attributesCount, indicesToUse);
		}

		double sumW = 0.0;
		for (int j = 0; j < indicesCount; j++)
		{
			double output = h->calculateOutput(X[indices[j]], attributesCount);

			weights[indices[j]] = weights[indices[j]] * exp(-(D[indices[j]] * output));
			sumW += weights[indices[j]];
		}
		for (int j = 0; j < indicesCount; j++)
			weights[indices[j]] = weights[indices[j]] / sumW;

		ht[stages] = h;
		stages++;

		delete[] trimingIndices;
	}

	void removeStage() override
	{
		if (stages > 0 && ht != nullptr)
		{
			stages -= 1;
			delete ht[stages];

			delete[] features;
			unordered_set<int> fet;
			for (int i = 0; i < stages; i++)
			{
				auto[fetNum, feat] = ht[i]->getFeatures();
				for (int j = 0; j < fetNum; j++)
					fet.insert(feat[j]);
			}

			featuresCount = (int)fet.size();
			features = new int[featuresCount];
			int i = 0;
			for (auto feature : fet)
			{
				features[i] = feature;
				i++;
			}
		}
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy próbki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attributes) const override
	{
		double out = 0.0;
		for (int k = 0; k < stages; k++)
		{
			out += ht[k]->calculateOutput(X, attributes);
		}
		return out;
	}
};

/// <summary>Klasyfiaktor AdaBoost</summary>
class AdaBoost : public Boosting
{
private:
	double* alphas = nullptr; /// <summary>Wagi poszczegolnych slabych klasyfikatorow </summary>
	ClassifierParameters parameters; /// <summary>Struktura z parametrami dla klasyfiaktorow</summary>
	string classifierType; /// <summary>Typ s³abego klasyfikatora</summary>

	int** sortedIndices = nullptr;
	double** minmax = nullptr;
	int** xInBin = nullptr;

	bool isStump = false;
	bool useBins = false;
public:
	using Classifier::train;
	using Boosting::addStage;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::loadModel;
	using Classifier::saveModel;

	~AdaBoost()
	{
		if (alphas != nullptr)
			delete[] alphas;
	}

	/// <summary>Utworzenie AdaBoosta na podstawie domyslnych parametrow</summary>
	AdaBoost()
	{
		this->classifierType = parameters.classifierType;
		this->T = parameters.boostingStages;
		this->useTrimming = parameters.useWeightTrimming;
		this->trimminingThreshold = parameters.weightTrimmingThreshold;
		this->trimmingMinSamples = parameters.weightTrimmingMinSamples;

		this->isStump = classifierType == DecisionStump::GetType();
		this->useBins = classifierType == RegularBins::GetType() || classifierType == BinnedDecisionStump::GetType() || classifierType == BinnedTree::GetType();
	}

	/// <summary>Utworzenie AdaBoosta na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	AdaBoost(const ClassifierParameters &parameters)
	{
		this->parameters = parameters;
		this->classifierType = parameters.classifierType;
		this->T = parameters.boostingStages;
		this->useTrimming = parameters.useWeightTrimming;
		this->trimminingThreshold = parameters.weightTrimmingThreshold;
		this->trimmingMinSamples = parameters.weightTrimmingMinSamples;

		this->isStump = classifierType == DecisionStump::GetType();
		this->useBins = classifierType == RegularBins::GetType() || classifierType == BinnedDecisionStump::GetType() || classifierType == BinnedTree::GetType();
	}

	/// <summary>Utworzenie RealBoosta na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	AdaBoost(const AdaBoost *toCopy)
	{
		this->parameters = toCopy->parameters;
		this->classifierType = toCopy->classifierType;
		this->T = toCopy->T;
		this->stages = toCopy->stages;
		this->useTrimming = toCopy->useTrimming;
		this->trimminingThreshold = toCopy->trimminingThreshold;
		this->trimmingMinSamples = toCopy->trimmingMinSamples;

		this->isStump = toCopy->isStump;
		this->useBins = toCopy->useBins;

		this->featuresCount = toCopy->featuresCount;
		this->features = new int[featuresCount];
		for (int i = 0; i < featuresCount; i++)
			this->features[i] = toCopy->features[i];

		if (stages > 0)
		{
			this->ht = new BoostableClassifier*[T];
			this->alphas = new double[T];
			for (int i = 0; i < stages; i++)
			{
				ht[i] = (InitalizeWeakClassifier(toCopy->ht[i]));
				alphas[i] = toCopy->alphas[i];
			}
		}
	}

	/// <summary>Zaladowanie AdaBoosta z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	AdaBoost(string path) { loadModel(path); }

	/// <summary>Zaladowanie AdaBoosta z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	AdaBoost(ifstream &input) { loadModel(input); }

	/// <summary>Zaladowanie DecisionStump-a  z podanego strumienia oraz zapisanie parametrow w odpowiedniej strukturze</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'params'>Struktura z parametrami dla klasyfiaktora</param>
	AdaBoost(ifstream &input, ClassifierParameters &params) { loadModel(input, params); }

	/// <summary>Zwraca instacje slabego klasyfikatora</summary>
	/// <returns>Instacja s³abego  klasyfikatora</returns>
	BoostableClassifier* InitalizeWeakClassifier()
	{
		if (classifierType == WeakPerceptron::GetType())
			return new WeakPerceptron(parameters);
		else if (classifierType == DecisionStump::GetType())
			return new DecisionStump(parameters);
		else if (classifierType == BinnedDecisionStump::GetType())
			return new BinnedDecisionStump(parameters);
		else if (classifierType == RegularBins::GetType())
			return new RegularBins(parameters);
		else if (classifierType == BinnedTree::GetType())
			return new BinnedTree(parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	BoostableClassifier* InitalizeWeakClassifier(BoostableClassifier* toCopy)
	{
		if (classifierType == WeakPerceptron::GetType())
			return new WeakPerceptron((WeakPerceptron*)toCopy);
		else if (classifierType == DecisionStump::GetType())
			return new DecisionStump((DecisionStump*)toCopy);
		else if (classifierType == BinnedDecisionStump::GetType())
			return new BinnedDecisionStump((BinnedDecisionStump*)toCopy);
		else if (classifierType == RegularBins::GetType())
			return new RegularBins((RegularBins*)toCopy);
		else if (classifierType == BinnedTree::GetType())
			return new BinnedTree((BinnedTree*)toCopy);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	void initializeData(const double* const* X, const int* indices, int samplesCount, int attributesCount, int indicesCount) override
	{
		if (!useTrimming && isStump && sortedIndices == nullptr)
		{
			sortedIndices = new int*[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int i = 0; i < attributesCount; i++)
			{
				sortedIndices[i] = new int[indicesCount];
				memcpy(sortedIndices[i], indices, indicesCount * sizeof(int));
				sort_indexes(X, sortedIndices[i], indicesCount, i);
			}
		}
		if (useBins && xInBin == nullptr)
		{
			minmax = new double*[attributesCount];
			for (int i = 0; i < attributesCount; i++)
				minmax[i] = new double[2];

			xInBin = new int*[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				xInBin[indices[i]] = new int[attributesCount];

			calculateRanges(X, minmax, indices, indicesCount, attributesCount, parameters.outlayerPercent);
			assignBins(X, minmax, xInBin, indices, indicesCount, attributesCount, parameters.treeBins);
		}
	}

	void clearData(const int* indices, int attributesCount, int indicesCount) override
	{
		if (sortedIndices != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] sortedIndices[i];
			delete[] sortedIndices;
			sortedIndices = nullptr;
		}
		if (xInBin != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] minmax[i];
			delete[] minmax;
			minmax = nullptr;

			for (int i = 0; i < indicesCount; i++)
				delete[] xInBin[indices[i]];
			delete[] xInBin;
			xInBin = nullptr;
		}
	}

	void endStagewiseTraining(const int* indices, int attributesCount, int indicesCount) override
	{
		if (sortedIndices != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] sortedIndices[i];
			delete[] sortedIndices;
			sortedIndices = nullptr;
		}
		if (xInBin != nullptr)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] minmax[i];
			delete[] minmax;
			minmax = nullptr;

			for (int i = 0; i < indicesCount; i++)
				delete[] xInBin[indices[i]];
			delete[] xInBin;
			xInBin = nullptr;
		}

		if (features != nullptr)
			delete[] features;

		int totalFeatures = stages;
		if (classifierType == BinnedTree::GetType())
			totalFeatures *= (int)(pow(2, parameters.maxTreeLevel) - 1);
		features = new int[totalFeatures];

		unordered_set<int> uniqueFeat;
		int f2 = 0;
		for (int i = 0; i < stages; i++)
		{
			auto[fetNum, feat] = ht[i]->getFeatures();
			for (int j = 0; j < fetNum; j++)
			{
				int feature = feat[j];
				if (uniqueFeat.count(feature) == 0)
				{
					features[f2] = feature;
					uniqueFeat.insert(feature);
					f2++;

				}
			}
		}
		featuresCount = f2;
	}

	/// <summary>Zwraca instacje slabego klasyfikatora utworzonego na podstawie danych z pliku</summary>
	/// <param name = 'input'>Strumien do pliku, w ktortm zostal zapisany slaby klasyfikator</param>
	/// <returns>Instacja s³abego  klasyfikatora</returns>
	BoostableClassifier* InitalizeWeakClassifier(ifstream &input)
	{
		if (classifierType == WeakPerceptron::GetType())
			return new WeakPerceptron(input, parameters);
		else if (classifierType == DecisionStump::GetType())
			return new DecisionStump(input, parameters);
		else if (classifierType == BinnedDecisionStump::GetType())
			return new BinnedDecisionStump(input, parameters);
		else if (classifierType == RegularBins::GetType())
			return new RegularBins(input, parameters);
		else if (classifierType == BinnedTree::GetType())
			return new BinnedTree(input, parameters);
		else
			throw ERRORS::NOT_IMPLEMENTED;
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "AdaBoost";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <param name = 'full'>Pe³ny/Skrócony opis klasyfikatora</param>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = "";
		text += getType() + "\r\n";
		text += classifierType + "\r\n";
		text += "Use Weight Trimming:" + to_string(useTrimming) + "\r\n";
		text += "Trimmining Threshold:" + to_string(trimminingThreshold) + "\r\n";
		text += "Trimming Minimum Samples:" + to_string(trimmingMinSamples) + "\r\n";
		text += "Stages:" + to_string(stages) + "\r\n";
		for (int i = 0; i < stages; i++)
		{
			text += "------\r\n";
			text += to_string(i) + ": \r\n";
			text += "Alpha: " + to_string(alphas[i]) + "\r\n";
			text += "WeakClassifier:\r\n";
			text += ht[i]->toString() + "\r\n";
		}
		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream &input) override
	{
		if (ht != nullptr)
		{
			for (int i = 0; i < stages; i++)
				delete ht[i];
			delete[] ht;
		}
		if (alphas != nullptr)
			delete[] alphas;
		if (features != nullptr)
			delete[] features;

		string fieldName, type;
		skipHeader(input);
		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> stages;
			input >> fieldName >> classifierType;
			input >> fieldName >> featuresCount;

			alphas = new double[stages];
			ht = new BoostableClassifier*[stages];
			features = new int[featuresCount];
			for (int i = 0; i < featuresCount; i++)
				input >> features[i];

			skipHeader(input);
			input >> fieldName >> useTrimming;
			if (useTrimming)
			{
				input >> fieldName >> trimminingThreshold;
				input >> fieldName >> trimmingMinSamples;
			}

			skipHeader(input);
			for (int i = 0; i < stages; i++)
			{
				skipHeader(input);
				input >> fieldName >> alphas[i];
				ht[i] = InitalizeWeakClassifier(input);
			}
			T = stages;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;

		parameters.boostingStages = T;
		strncpy_s(parameters.boostingType, ClassifierParameters::STRING_BUFFER, type.c_str(), _TRUNCATE);
		strncpy_s(parameters.classifierType, ClassifierParameters::STRING_BUFFER, classifierType.c_str(), _TRUNCATE);
		parameters.useWeightTrimming = useTrimming;
		parameters.weightTrimmingThreshold = trimminingThreshold;
		parameters.weightTrimmingMinSamples = trimmingMinSamples;

		this->isStump = classifierType == DecisionStump::GetType();
		this->useBins = classifierType == RegularBins::GetType() || classifierType == BinnedDecisionStump::GetType() || classifierType == BinnedTree::GetType();
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	/// <param name = 'param'>Struktura z parametrami dla boostingu</param>
	void loadModel(ifstream &input, ClassifierParameters &params) override
	{
		loadModel(input);

		// TODO parametry slabych klasyfikatorow

		params.boostingStages = T;
		strncpy_s(parameters.boostingType, ClassifierParameters::STRING_BUFFER, getType().c_str(), _TRUNCATE);
		strncpy_s(parameters.classifierType, ClassifierParameters::STRING_BUFFER, classifierType.c_str(), _TRUNCATE);
		params.useWeightTrimming = useTrimming;
		params.weightTrimmingThreshold = trimminingThreshold;
		params.weightTrimmingMinSamples = trimmingMinSamples;
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream &output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Stages: " << stages << endl;
		output << "Weak_Classifier_Type: " << classifierType << endl;
		output << "Features_Count: " << featuresCount << endl;
		for (int i = 0; i < featuresCount; i++)
			output << features[i] << " ";
		output << endl;
		createSecondaryHeader(output, "Training_Parameters:");
		output << "Using_Weight_Trimming: " << useTrimming << endl;
		if (useTrimming)
		{
			output << "Trimming_Threshold: " << trimminingThreshold << endl;
			output << "Trimming_Minimum_Samples: " << trimmingMinSamples << endl;
		}
		createSecondaryHeader(output, "Weak_Classifiers:");
		for (int i = 0; i < stages; i++)
		{
			createSecondaryHeader(output, "Weak_Classifier_" + to_string(i));
			output << "Weight: " << alphas[i] << endl;
			ht[i]->saveModel(output);
		}
	}

	void saveModelOld(ofstream &output) const override
	{
		output << getType() << endl;
		output << useTrimming << endl;
		output << trimminingThreshold << endl;
		output << trimmingMinSamples << endl;
		output << stages << endl;
		output << classifierType << endl;
		for (int i = 0; i < stages; i++)
		{
			output << alphas[i] << endl;
			ht[i]->saveModelOld(output);
		}
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'indices'>Indeksy probek bioracych udzial w uczeniu</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const double* const* X, const int* D, const int* indices, int samplesCount, int attributesCount, int indicesCount) override
	{
		bool sortedData = false;
		bool assignedBins = false;

		if (ht != nullptr)
		{
			for (int i = 0; i < stages; i++)
				delete ht[i];
			delete[] ht;
		}
		if (alphas != nullptr)
			delete[] alphas;
		if (features != nullptr)
			delete[] features;
		stages = T;

		alphas = new double[T];
		ht = new BoostableClassifier*[T];

#pragma region Clasifier Parameters Initialization
		int* trimingIndices = new int[indicesCount];
		memcpy(trimingIndices, indices, indicesCount * sizeof(int));
		int indicesToUse = indicesCount;

		if (!useTrimming && isStump && sortedIndices == nullptr)
		{
			sortedData = true;
			sortedIndices = new int*[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int i = 0; i < attributesCount; i++)
			{
				sortedIndices[i] = new int[indicesCount];
				memcpy(sortedIndices[i], indices, indicesCount * sizeof(int));
				sort_indexes(X, sortedIndices[i], indicesCount, i);
			}
		}
		if (useBins && xInBin == nullptr)
		{
			assignedBins = true;
			minmax = new double*[attributesCount];
			for (int i = 0; i < attributesCount; i++)
				minmax[i] = new double[2];

			xInBin = new int*[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				xInBin[indices[i]] = new int[attributesCount];

			calculateRanges(X, minmax, indices, indicesCount, attributesCount, parameters.outlayerPercent);
			assignBins(X, minmax, xInBin, indices, indicesCount, attributesCount, parameters.treeBins);
		}
#pragma endregion

		double* w = new double[samplesCount];
		for (int i = 0; i < indicesCount; i++)
			w[indices[i]] = (1.0 / indicesCount);

		unordered_set<int> fet;
		// Kolejne etapy boostingu
		for (int i = 0; i < T; i++)
		{
			BoostableClassifier *h = InitalizeWeakClassifier();
			if (!useTrimming && isStump)
				((DecisionStump *)h)->train(X, D, sortedIndices, w, samplesCount, attributesCount, indicesCount);
			else if (useBins)
				((BoostableBinnedClassifier *)h)->train(xInBin, minmax, D, trimingIndices, w, samplesCount, attributesCount, indicesToUse);
			else
				h->train(X, D, trimingIndices, w, samplesCount, attributesCount, indicesToUse);

			double err = 0.0;
			int* outErr = new int[samplesCount];
			for (int k = 0; k < indicesCount; k++)
			{
				int out = h->classify(X[indices[k]], attributesCount);
				outErr[indices[k]] = (int)(out != D[indices[k]]);
				err += outErr[indices[k]] * w[indices[k]];
			}

			double probabilityQuotient = (1.0 - err) / err;
			if (err == 0)
				alphas[i] = 2.0;
			else if (probabilityQuotient < exp(-4))
				alphas[i] = -2.0;
			else if (probabilityQuotient > exp(4))
				alphas[i] = 2.0;
			else
				alphas[i] = 0.5 * log(probabilityQuotient);

			double sumW = 0.0;
			for (int j = 0; j < indicesCount; j++)
			{
				if (outErr[indices[j]] == 1)
					w[indices[j]] = w[indices[j]] * exp(alphas[i]);
				else
					w[indices[j]] = w[indices[j]] * exp(-1 * alphas[i]);
				sumW += w[indices[j]];
			}
			for (int j = 0; j < indicesCount; j++)
				w[indices[j]] = w[indices[j]] / sumW;

			if (useTrimming)
			{
				memcpy(trimingIndices, indices, indicesCount * sizeof(int));
				sort_indexes_dsc(w, trimingIndices, indicesCount);

				double sum = 0;
				unsigned int z = 0;
				for (; z < (unsigned int)indicesCount; z++)
				{
					sum += w[trimingIndices[z]];
					if (sum >= trimminingThreshold && z >= (unsigned int)(trimmingMinSamples * indicesCount))
						break;
				}
				indicesToUse = z;
			}

			auto[fetNum, feat] = h->getFeatures();
			for (int i = 0; i < fetNum; i++)
				fet.insert(feat[i]);

			ht[i] = h;
			delete[] outErr;
		}

		featuresCount = (int)fet.size();
		features = new int[featuresCount];
		int i = 0;
		for (auto feature : fet)
		{
			features[i] = feature;
			i++;
		}

		if (sortedData)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] sortedIndices[i];
			delete[] sortedIndices;
			sortedIndices = nullptr;
		}
		if (assignedBins)
		{
			for (int i = 0; i < attributesCount; i++)
				delete[] minmax[i];
			delete[] minmax;
			minmax = nullptr;

			for (int i = 0; i < indicesCount; i++)
				delete[] xInBin[indices[i]];
			delete[] xInBin;
			xInBin = nullptr;
		}
		delete[] w;
		delete[] trimingIndices;
	}

	/// <summary>Dodaje nowy etap do silnego klasyfikaotra</summary>
	/// <param name = 'X'>Cechy próbek ucz¹cych</param>
	/// <param name = 'D'>Klasy próbek ucz¹cych</param>
	/// <param name = 'w'>Aktualne wagi probek</param>
	/// <param name = 'Indices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	void addStage(const double* const* X, const int* D, const int* indices, double* &weights, int samplesCount, int attributesCount, int indicesCount) override
	{
		if (stages >= T)
		{
			double* alphas_tmp = alphas;
			BoostableClassifier** ht_tmp = ht;

			alphas = new double[T + 1];
			ht = new BoostableClassifier*[T + 1];

			memcpy(alphas, alphas_tmp, T * sizeof(double));
			memcpy(ht, ht_tmp, T * sizeof(BoostableClassifier *));

			T++;
			delete[] alphas_tmp;
			delete[] ht_tmp;
		}

		if (!useTrimming && isStump && sortedIndices == nullptr)
		{
			sortedIndices = new int*[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int i = 0; i < attributesCount; i++)
			{
				sortedIndices[i] = new int[indicesCount];
				memcpy(sortedIndices[i], indices, indicesCount * sizeof(int));
				sort_indexes(X, sortedIndices[i], indicesCount, i);
			}
		}
		if (useBins && xInBin == nullptr)
		{
			minmax = new double*[attributesCount];
			for (int i = 0; i < attributesCount; i++)
				minmax[i] = new double[2];

			xInBin = new int*[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				xInBin[indices[i]] = new int[attributesCount];

			calculateRanges(X, minmax, indices, indicesCount, attributesCount, parameters.outlayerPercent);
			assignBins(X, minmax, xInBin, indices, indicesCount, attributesCount, parameters.treeBins);
		}

		int* trimingIndices = nullptr;
		int indicesToUse = indicesCount;

		if (useTrimming)
		{
			trimingIndices = new int[indicesCount];
			memcpy(trimingIndices, indices, indicesCount * sizeof(int));
		}

		if (weights == nullptr)
		{
			stages = 0;

			if (ht != nullptr)
			{
				for (int i = 0; i < T; i++)
					delete ht[i];
				delete[] ht;
			}
			if (alphas != nullptr)
				delete[] alphas;

			alphas = new double[T];
			ht = new BoostableClassifier*[T];

			weights = new double[samplesCount];
			for (int i = 0; i < indicesCount; i++)
				weights[indices[i]] = (1.0 / indicesCount);
		}
		else if (useTrimming)
		{
			sort_indexes_dsc(weights, trimingIndices, indicesCount);

			double sum = 0;
			unsigned int z = 0;
			for (; z < (unsigned int)indicesCount; z++)
			{
				sum += weights[trimingIndices[z]];
				if (sum >= trimminingThreshold && z >= (unsigned int)(trimmingMinSamples * indicesCount))
					break;
			}
			indicesToUse = z;
		}

		BoostableClassifier *h = InitalizeWeakClassifier();
		if (!useTrimming)
		{
			if (sortedIndices != nullptr && isStump)
				((DecisionStump *)h)->train(X, D, sortedIndices, weights, samplesCount, attributesCount, indicesCount);
			else if (xInBin != nullptr && useBins)
				((BoostableBinnedClassifier *)h)->train(xInBin, minmax, D, indices, weights, samplesCount, attributesCount, indicesToUse);
			else
				h->train(X, D, indices, weights, samplesCount, attributesCount, indicesToUse);
		}
		if (useTrimming)
		{
			if (xInBin != nullptr && useBins)
				((BoostableBinnedClassifier *)h)->train(xInBin, minmax, D, trimingIndices, weights, samplesCount, attributesCount, indicesToUse);
			else
				h->train(X, D, trimingIndices, weights, samplesCount, attributesCount, indicesToUse);
		}

		double err = 0.0;
		int* outErr = new int[samplesCount];
		for (int k = 0; k < indicesCount; k++)
		{
			int out = h->classify(X[indices[k]], attributesCount);
			outErr[indices[k]] = (int)(out != D[indices[k]]);
			err += outErr[indices[k]] * weights[indices[k]];
		}

		double probabilityQuotient = (1.0 - err) / err;
		if (err == 0)
			alphas[stages] = 2.0;
		else if (probabilityQuotient < exp(-4))
			alphas[stages] = -2.0;
		else if (probabilityQuotient > exp(4))
			alphas[stages] = 2.0;
		else
			alphas[stages] = 0.5 * log(probabilityQuotient);

		double sumW = 0.0;
		for (int j = 0; j < indicesCount; j++)
		{
			if (outErr[indices[j]] == 1)
				weights[indices[j]] = weights[indices[j]] * exp(alphas[stages]);
			else
				weights[indices[j]] = weights[indices[j]] * exp(-1 * alphas[stages]);
			sumW += weights[indices[j]];
		}
		for (int j = 0; j < indicesCount; j++)
			weights[indices[j]] = weights[indices[j]] / sumW;

		ht[stages] = h;
		stages++;

		delete[] outErr;
		delete[] trimingIndices;
	}

	void removeStage() override
	{
		if (stages > 0 && ht != nullptr)
		{
			stages -= 1;
			delete ht[stages];

			delete[] features;
			unordered_set<int> fet;
			for (int i = 0; i < stages; i++)
			{
				auto[fetNum, feat] = ht[i]->getFeatures();
				for (int j = 0; j < fetNum; j++)
					fet.insert(feat[j]);
			}

			featuresCount = (int)fet.size();
			features = new int[featuresCount];
			int i = 0;
			for (auto feature : fet)
			{
				features[i] = feature;
				i++;
			}
		}
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy próbki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attributes) const override
	{
		double out = 0.0;

		for (int k = 0; k < stages; k++)
		{
			int y = ht[k]->classify(X, attributes);
			out += alphas[k] * y;
		}

		return out;
	}
};