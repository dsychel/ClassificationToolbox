#pragma once
#include<iostream>
#include<unordered_set>
#include<omp.h>
#include<tuple>
#include<string>

#include"utills.h"
#include"configuration.h"
#include"feature.h"

using namespace std;

#pragma region ImpurityMetrices
inline double GiniImpurity(double positivesProbability, double positivesWeightSum)
{
	return 2.0 * positivesWeightSum * (1.0 - positivesProbability);
}

inline double GiniIndex(double positivesWeightsLeftSum, double weightsSumLeft, double positivesWeightsRightSum, double weightsSumRight)
{
	double impurityLeft = 0.5;
	double impurityRight = 0.5;

	if (weightsSumLeft > 0.0)
	{
		double prOneL = positivesWeightsLeftSum / weightsSumLeft;
		impurityLeft = GiniImpurity(prOneL, positivesWeightsLeftSum);
	}
	if (weightsSumRight > 0.0)
	{
		double prOneR = positivesWeightsRightSum / weightsSumRight;
		impurityRight = GiniImpurity(prOneR, positivesWeightsRightSum);
	}

	return impurityLeft + impurityRight;
}

inline double Entrophy(double positivesProbability)
{
	return -(positivesProbability * log(positivesProbability) + (1.0 - positivesProbability) * log(1.0 - positivesProbability));
}

inline double InformationGain(double positivesWeightsLeftSum, double weightsSumLeft, double positivesWeightsRightSum, double weightsSumRight)
{
	double weightSum = weightsSumLeft + weightsSumRight;
	double onesWeightSum = positivesWeightsLeftSum + positivesWeightsRightSum;
	double prOneL = positivesWeightsLeftSum / weightsSumLeft;
	double prOneR = positivesWeightsRightSum / weightsSumRight;
	double prOne = onesWeightSum / weightSum;

	return -(Entrophy(prOne) - Entrophy(prOneL) - Entrophy(prOneR));
}
#pragma endregion Impurity Metrices

/// <summary>Klasa bazowa dla wszystkich klasyfikatorow</summary>
class Classifier
{
protected:
	int featuresCount = 0;
	int* features = nullptr; /// <summary>Lista wybranych cech</summary>

	/// <summary>Ominiecie naglowka sekcji</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	static void skipHeader(ifstream& input)
	{
		string ignore;
		for (int i = 0; i < 3; i++)
			input >> ignore;
	}

	/// <summary>Dodanie naglowka do pliku</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	/// <param name = 'header'>Naglowek</param>
	static void createMainHeader(ofstream& output, string header)
	{
		output << "-----------------------------------" << endl;
		output << header << endl;
		output << "-----------------------------------" << endl;
	}

	/// <summary>Dodanie naglowka do pliku</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	/// <param name = 'header'>Naglowek</param>
	static void createSecondaryHeader(ofstream& output, string header)
	{
		output << "------------------------" << endl;
		output << header << endl;
		output << "------------------------" << endl;
	}

public:
	virtual ~Classifier()
	{
		if (features != nullptr)
			delete[] features;
	};

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <returns>Opis klasyfikatora</returns>
	virtual string toString() const = 0;

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	virtual string getType() const = 0;

	virtual bool isCascade() { return false; }

	/// <summary>Zwraca wybrane w wyniku nauki cechy (FeaturesCount, Features)</summary>
	/// <returns>FeaturesCount, Features</returns>
	tuple<int, const int*> getFeatures() { return make_tuple(featuresCount, features); }

	int getFeauresCount() { return featuresCount; }

	/// <summary>Zaladowanie modelu z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	void loadModel(string path)
	{
		ifstream input = ifstream(path);
		loadModel(input);
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	virtual void loadModel(ifstream& input) = 0;

	virtual void loadModel(ifstream& input, ClassifierParameters& parameters) = 0;

	/// <summary>Zapisanie modelu do pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	virtual void saveModel(string path) const
	{
		ofstream output = ofstream(path);
		output.precision(16);
		saveModel(output);
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	virtual void saveModel(ofstream& output) const = 0;

	virtual void saveModelOld(string path) const
	{
		ofstream output = ofstream(path);
		output.precision(16);
		saveModelOld(output);
	}

	virtual void saveModelOld(ofstream& output) const = 0;

	virtual void train(const double* const* X, const int* D, const int* indices, int samplesCount, int attributesCount, int indicesCount) = 0;

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	virtual void train(const double* const* X, const int* D, int samplesCount, int attributesCount)
	{
		int* indices = new int[samplesCount];
		for (int i = 0; i < samplesCount; i++)
			indices[i] = i;

		train(X, D, indices, samplesCount, attributesCount, samplesCount);

		delete[] indices;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'traingPath'>Sciezka do pliku z probkami</param>
	void train(const string& traingPath)
	{
		auto [X, D, samples, attributes] = readBinary(traingPath);
		train(X, D, samples, attributes);

		clearData(X, D, samples);
	}

	/// <summary>Walidacja klasyfikatora (Accurancy, FAR, Sensitivity)</summary>
	/// <param name = 'X'>Probki do walidacji</param>
	/// <param name = 'D'>Klasy do walidacji</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	/// <returns>Accurancy, FAR, Sensitivity</returns>
	virtual tuple<const double, const double, const double> validate(const double* const* X, const int* D, int samples, int attributes, double threshold = 0.0) const
	{
		int* Y = classify(X, samples, attributes, threshold);
		int TP = 0, TN = 0, FP = 0, FN = 0;
		for (int i = 0; i < samples; i++)
		{
			if (D[i] == Y[i] && D[i] == 1)
				TP++;
			else if (D[i] == Y[i] && D[i] == -1)
				TN++;
			else if (D[i] != Y[i] && D[i] == 1 && Y[i] == -1)
				FN++;
			else
				FP++;
		}
		delete[] Y;

		double far = 1.0 * (FP) / (FP + TN);
		double sensitivity = 1.0 * (TP) / (TP + FN);
		double accuracy = 1.0 * (TP + TN) / (TP + TN + FP + FN);

		return make_tuple(accuracy, far, sensitivity);
	}

	/// <summary>Walidacja klasyfikatora (Accurancy, FAR, Sensitivity)</summary>
	/// <param name = 'validationPath'>Sciezka do pliku z probkami</param>
	/// <returns>Accurancy, FAR, Sensitivity </returns>
	virtual tuple<const double, const double, const double> validate(const string& validationPath, double threshold = 0.0) const
	{
		auto [X, D, samples, attributes] = readBinary(validationPath);
		auto ret = validate(X, D, samples, attributes, threshold);

		clearData(X, D, samples);

		return ret;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania</summary>
	/// <param name = 'X'>Probka</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	virtual inline double calculateOutput(const double* X, int attributesCount) const = 0;

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania</summary>
	/// <param name = 'X'>Probki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	/// <returns>Tablica odpowiedzi klasyfikatora</returns>
	double* calculateOutput(const double* const* X, int samplesCount, int attributesCount) const
	{
		double* out = new double[samplesCount];
#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < samplesCount; i++)
			out[i] = calculateOutput(X[i], attributesCount);

		return out;
	}

	virtual inline tuple<double, int> calculateOutputN(const double* X, int attributesCount) const
	{
		return make_tuple(calculateOutput(X, attributesCount), featuresCount);
	}

	virtual tuple<double*, double> calculateOutputN(const double* const* X, int samplesCount, int attributesCount) const
	{
		double* out = new double[samplesCount];
		int* fet = new int[samplesCount];
#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < samplesCount; i++)
		{
			int features = 0;
			tie(out[i], fet[i]) = calculateOutputN(X[i], attributesCount);
		}

		double avgFeatures = 0;
		for (int i = 0; i < samplesCount; i++)
			avgFeatures += fet[i];
		avgFeatures /= samplesCount;
		delete[] fet;

		return make_tuple(out, avgFeatures);
	}

	double* calculateOutput(const double* const* X, const int* indices, int samplesCount, int attributesCount, int indicesCount) const
	{
		double* out = new double[samplesCount];
#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < indicesCount; i++)
			out[indices[i]] = calculateOutput(X[indices[i]], attributesCount);

		return out;
	}

	/// <summary>Klasyfikacja podnaego okna obrazu</summary>
	virtual tuple<double, int> calculateOutputForWindowN(Extractor* ext, int wx, int wy, int x, int y, double* attributes) const
	{
		//double * attributes = new double[ext->getFeaturesCount()];
		int fc = ext->extractFromWindow(attributes, features, featuresCount, wx, wy, x, y);
		double out = this->calculateOutput(attributes, fc);
		//delete[] attributes;

		return make_tuple(out, featuresCount);
	}

	/// <summary>Klasyfikacja podnaego okna obrazu</summary>
	virtual double calculateOutputForWindow(Extractor* ext, int wx, int wy, int x, int y, double* attributes) const
	{
		//double * attributes = new double[ext->getFeaturesCount()];
		int fc = ext->extractFromWindow(attributes, features, featuresCount, wx, wy, x, y);
		double out = this->calculateOutput(attributes, fc);
		//delete[] attributes;

		return out;
	}

	//virtual inline int classifyOutputForWindow(Extractor *ext, int wx, int wy, int x, int y, double* features, double threshold = 0.0) const
	//{
	//	if (calculateOutputForWindow(ext, wx, wy, x, y, features) >= threshold)
	//		return 1;
	//	else
	//		return -1;
	//}

	/// <summary>Klasyfikacja probki przy podanym progu</summary>
	/// <param name = 'X'>Probka</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	/// <param name = 'threshold'>Prog decyzyjny, domyslnie 0</param>
	/// <returns>Klasa podanej probki</returns>
	virtual inline int classify(const double* X, int attributesCount, double threshold = 0.0) const
	{
		if (calculateOutput(X, attributesCount) >= threshold)
			return 1;
		else
			return -1;
	}

	/// <summary>Klasyfikacja probek przy podanym progu</summary>
	/// <param name = 'X'>Probki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	/// <param name = 'threshold'>Prog decyzyjny, domyslnie 0</param>
	/// <returns>Tablica klas dla podanych probek</returns>
	int* classify(const double* const* X, int samplesCount, int attributesCount, double threshold = 0.0) const
	{
		int* D = new int[samplesCount];
#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < samplesCount; i++)
			D[i] = classify(X[i], attributesCount, threshold);

		return D;
	}

	int* classify(const double* const* X, const int* indices, int samplesCount, int attributesCount, int indicesCount, double threshold = 0.0) const
	{
		int* D = new int[samplesCount];
#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < indicesCount; i++)
			D[indices[i]] = classify(X[indices[i]], attributesCount, threshold);

		return D;
	}
};

/// <summary>Klasa bazowa dla klasyfikatorow, ktore moga zostac wykorzystane w algorytmach boostingowych</summary>
class BoostableClassifier : public Classifier
{
public:
	using Classifier::train;

	virtual bool isUsingBins() const { return false; }

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'boostingWeights'>Wagi dla boostingu</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	virtual void train(const double* const* X, const int* D, const int* indices, const double* boostingWeights, int samplesCount, int attributesCount, int indicesCount) = 0;

	virtual void train(const double* const* X, const int* D, const double* boostingWeights, int samplesCount, int attributesCount)
	{
		int* indices = new int[samplesCount];
		for (int i = 0; i < samplesCount; i++)
			indices[i] = i;

		train(X, D, indices, boostingWeights, samplesCount, attributesCount, samplesCount);

		delete[] indices;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	virtual void train(const double* const* X, const int* D, const int* indices, int samplesCount, int attributesCount, int indicesCount) override
	{
		double* boostingWeights = new double[samplesCount];
		for (int i = 0; i < indicesCount; i++)
			boostingWeights[indices[i]] = 1.0 / indicesCount;

		train(X, D, indices, boostingWeights, samplesCount, attributesCount, indicesCount);

		delete[] boostingWeights;
	}
};

/// <summary>Klasa bazowa dla  klasyfikatorow rzeczywisto liczbowych z koszykami, ktore moga zostac wykorzystane w algorytmach boostingowych</summary>
class BoostableBinnedClassifier : public BoostableClassifier
{
protected:
	int B = 8; /// <summary>Liczba koszy</summary>
	double outlayerPercent = 0; /// <summary>Probki odstajace</summary>

public:
	using BoostableClassifier::train;

	bool isUsingBins() const override { return true; }

	virtual ~BoostableBinnedClassifier() {};

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'xInBin'>Numer kosza, do ktorego nalezy dana wartosc</param>
	/// <param name = 'xRanges'>Zakres cech</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'boostingWeights'>Wagi dla boostingu</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	virtual void train(const int* const* xInBin, const double* const* xRanges, const int* D, const int* indices, const double* boostingWeights, int samplesCount, int attributesCount, int indicesCount) = 0;

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	virtual void train(const double* const* X, const int* D, const int* indices, const double* boostingWeights, int samplesCount, int attributesCount, int indicesCount) override
	{
		double** xRanges = new double* [attributesCount];
		for (int i = 0; i < attributesCount; i++)
			xRanges[i] = new double[2];

		int** xInBin = new int* [samplesCount];
		for (int i = 0; i < indicesCount; i++)
			xInBin[indices[i]] = new int[attributesCount];

		calculateRanges(X, xRanges, indices, indicesCount, attributesCount, outlayerPercent);
		assignBins(X, xRanges, xInBin, indices, indicesCount, attributesCount, B);

		train(xInBin, xRanges, D, indices, boostingWeights, samplesCount, attributesCount, indicesCount);

		for (int i = 0; i < attributesCount; i++)
			delete[] xRanges[i];
		delete[] xRanges;

		for (int i = 0; i < indicesCount; i++)
			delete[] xInBin[indices[i]];
		delete[] xInBin;
	}
};

/// <summary>Klasyfikator zero rule - zwraca klase najczesciej wystepujaca w probie uczacej</summary>
class ZeroRule : public Classifier
{
private:
	int cls; /// <summary>Zwracana klasa</summary>

public:
	using Classifier::calculateOutput;
	using Classifier::classify;
	using Classifier::loadModel;
	using Classifier::saveModel;

	ZeroRule()
	{
		cls = 1;
		featuresCount = 0;
		features = nullptr;
	}

	ZeroRule(ZeroRule* toCopy)
	{
		this->cls = toCopy->cls;
		this->features = nullptr;
		this->featuresCount = 0;
	}

	/// <summary>Zaladowanie ZeroRula z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	ZeroRule(string path)
	{
		loadModel(path);
		features = nullptr;
	}

	/// <summary>Zaladowanie ZeroRula z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	ZeroRule(ifstream& input)
	{
		loadModel(input);
		features = nullptr;
	}

	ZeroRule(ifstream& input, ClassifierParameters& parameter)
	{
		loadModel(input, parameter);
		features = nullptr;
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = "";
		text += getType() + "\r\n";
		text += "Class: " + to_string(cls) + "\r\n";

		return text;
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	static string GetType()
	{
		return "ZeroRule";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		string fieldName, type;

		skipHeader(input);

		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> featuresCount;
			input >> fieldName >> cls;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
	}

	void loadModel(ifstream& input, ClassifierParameters& parameters) override
	{
		loadModel(input);
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Features_Count: " << featuresCount << endl;
		output << "Class: " << cls << endl;
	}

	void saveModelOld(ofstream& output) const override
	{
		output << getType() << endl;
		output << cls << endl;;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const double* const* X, const int* D, const int* indices, int samplesCount, int attributesCount, int indicesCount) override
	{
		int minus = 0, plus = 0;
		for (int i = 0; i < indicesCount; i++)
		{
			if (D[indices[i]] == -1)
				minus++;
			else if (D[indices[i]] == 1)
				plus++;
		}
		if (minus > plus)
			cls = -1;
		else
			cls = 1;
	}

	void train(const double* const* X, const int* D, int samplesCount, int attributesCount) override
	{
		int minus = 0, plus = 0;
		for (int i = 0; i < samplesCount; i++)
		{
			if (D[i] == -1)
				minus++;
			else
				plus++;
		}
		if (minus > plus)
			cls = -1;
		else
			cls = 1;
	}


	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania</summary>
	/// <param name = 'X'>Probka</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attributesCount) const override
	{
		return cls;
	}

	/// <summary>Klasyfikacja probki przy podanym progu</summary>
	/// <param name = 'X'>Probka</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	/// <param name = 'threshold'>Prog decyzyjny, domyslnie 0</param>
	/// <returns>Klasa podanej probki</returns>
	inline int classify(const double* X, int attributesCount, double threshold = 0.0) const override
	{
		int out = cls;
		if (threshold < 0)
			out = -out;
		return out;
	}
};

/// <summary>Jedno poziomowe drzewo decyzyjne</summary>
class DecisionStump : public BoostableClassifier
{
private:
	int direction; /// <summary>Kierunek mniejsze/wieksze</summary>
	double threshold; /// <summary>Prog podzialu</summary>
	double leftResponse;
	double rightResponse;

	/// <summary>Uczenie klasyfikatora (direction, threshold)</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'sortedIndices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	/// <param name = 'boostingWeight'>Wagi probek</param>
	/// <param name = 'indicesCount'>Liczba probek</param>
	/// <param name = 'feature'>Atrybut</param>
	tuple<int, double> train(const double* const* X, const int* D, const int* sortedIndices, int indicesCount, int feature, const double* boostingWeight)
	{
		double tmpThreshold;
		double err = INFINITY;

		int dir;
		double thr;

		// poczatkowy prog dla klasyfikatora
		thr = X[sortedIndices[0]][feature] - 0.0001;
		double errorLt = 0, errorGt = 0;
		for (int i = 0; i < indicesCount; i++)
			if (D[sortedIndices[i]] == 1)
				errorLt += boostingWeight[sortedIndices[i]];
		errorGt = 1 - errorLt;

		// Wybor miedzy znakiem mniejszosc a wiekszosci
		if (errorLt < err)
		{
			err = errorLt;
			dir = 1;
		}
		if (errorGt < err)
		{
			err = errorGt;
			dir = -1;
		}

		// Przeszukanie kolejnych progow
		for (int i = 0; i < indicesCount; i++)
		{
			// Przejscie po powtarzajacyh sie wartosciach cechy i uwzglednienie ich w bledzie
			while (i < indicesCount - 1 && X[sortedIndices[i]][feature] == X[sortedIndices[i + 1]][feature])
			{
				if (D[sortedIndices[i]] == 1)
					errorLt -= boostingWeight[sortedIndices[i]];
				else
					errorLt += boostingWeight[sortedIndices[i]];
				i++;
			}

			// Wstawienie progu miedzy dwie rozne wartosci
			if (i != indicesCount - 1)
				tmpThreshold = (X[sortedIndices[i]][feature] + X[sortedIndices[i + 1]][feature]) / 2.0;
			// Wstawienie progu za ostatnia probka
			else
				tmpThreshold = X[sortedIndices[i]][feature] + 0.0001;

			// Aktualizacja bledu
			if (D[sortedIndices[i]] == 1)
				errorLt -= boostingWeight[sortedIndices[i]];
			else
				errorLt += boostingWeight[sortedIndices[i]];
			errorGt = 1 - errorLt;

			// Wybor miedzy znakiem mniejszosc a wiekszosci
			if (errorLt < err)
			{
				err = errorLt;
				dir = 1;
				thr = tmpThreshold;
			}
			if (errorGt < err)
			{
				err = errorGt;
				dir = -1;
				thr = tmpThreshold;
			}
		}
		return make_tuple(dir, thr);
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Probka do klasyfikacji</param>
	/// <param name = 'attr'>Numer cechy</param>
	/// <param name = 'dir'>Kierunenk nierwonosci</param>
	/// <param name = 'thr'>Prog dla drzewa</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline int classify(const double* X, int attr, int dir, double thr) const
	{
		return (int)(dir * ((X[attr] < thr) - 0.5) * 2.0);
	}

public:
	using BoostableClassifier::train;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::loadModel;
	using Classifier::saveModel;

	/// <summary>Utworzenie DecisionStump-a na podstawie domyslnych parametrow</summary>
	DecisionStump()
	{
		featuresCount = 1;
		features = new int[featuresCount];
	}

	DecisionStump(ClassifierParameters& parameters)
	{
		featuresCount = 1;
		features = new int[featuresCount];
	}

	/// <summary>Utworzenie DecisionStump-a na podstawie domyslnych parametrow</summary>
	DecisionStump(DecisionStump* toCopy)
	{
		this->direction = toCopy->direction;
		this->threshold = toCopy->threshold;
		this->leftResponse = toCopy->leftResponse;
		this->rightResponse = toCopy->rightResponse;
		this->featuresCount = toCopy->featuresCount;
		this->features = new int[featuresCount];
		this->features[0] = toCopy->features[0];
	}

	/// <summary>Zaladowanie DecisionStump-a  z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	DecisionStump(string path)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(path);
	}

	/// <summary>Zaladowanie DecisionStump-a z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	DecisionStump(ifstream& input)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(input);
	}

	DecisionStump(ifstream& input, ClassifierParameters& parameters)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(input, parameters);
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "DecisionStump";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Feature Count: " + to_string(featuresCount) + "\r\n";
		text += "Feature: " + to_string(features[0]) + "\r\n";
		text += "Direction: " + to_string(direction) + "\r\n";
		text += "Threshold: " + to_string(threshold) + "\r\n";
		text += "Left Response: " + to_string(leftResponse) + "\r\n";
		text += "Right Response: " + to_string(rightResponse) + "\r\n";

		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		string fieldName, type;

		skipHeader(input);

		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> features[0];
			input >> fieldName >> direction;
			input >> fieldName >> threshold;
			input >> fieldName >> leftResponse;
			input >> fieldName >> rightResponse;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
	}

	void loadModel(ifstream& input, ClassifierParameters& parameters) override
	{
		loadModel(input);
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Feature: " << features[0] << endl;
		output << "Direction: " << direction << endl;
		output << "Threshold: " << threshold << endl;
		output << "Left_Response: " << leftResponse << endl;
		output << "Right_Response: " << rightResponse << endl;
	}

	void saveModelOld(ofstream& output) const override
	{
		output << getType() << endl;
		output << features[0] << endl;
		output << direction << endl;
		output << threshold << endl;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'sortedIndices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	/// <param name = 'boostingWeight'>Wagi probek</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const double* const* X, const int* D, const int* const* sortedIndices, const double* boostingWeight, int samplesCount, int attributesCount, int indicesCount)
	{
		double err;

		double* errors = new double[attributesCount];
		int* directions = new int[attributesCount];
		double* thresholds = new double[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int j = 0; j < attributesCount; j++)
		{
			auto [dir, thr] = train(X, D, sortedIndices[j], indicesCount, j, boostingWeight);
			directions[j] = dir;
			thresholds[j] = thr;

			double outErr = 0.0;
			for (int k = 0; k < indicesCount; k++)
			{
				int cls = classify(X[sortedIndices[j][k]], j, dir, thr);
				outErr += (int)(cls != D[sortedIndices[j][k]]) * boostingWeight[sortedIndices[j][k]];
			}
			errors[j] = outErr;
		}

		features[0] = 0;
		direction = directions[0];
		threshold = thresholds[0];
		err = errors[0];
		for (int j = 1; j < attributesCount; j++)
		{
			if (err > errors[j])
			{
				features[0] = j;
				direction = directions[j];
				threshold = thresholds[j];
				err = errors[j];
			}
		}

		double leftDenominator = 0.0, rightDenominator = 0.0, leftNumerator = 0.0, rightNumerator = 0.0;
		for (int i = 0; i < indicesCount; i++)
		{
			if (X[sortedIndices[features[0]][i]][features[0] < threshold])
			{
				leftDenominator += boostingWeight[sortedIndices[features[0]][i]];
				if (direction == 1 && D[sortedIndices[features[0]][i]] == 1)
					leftNumerator += boostingWeight[sortedIndices[features[0]][i]];
				else if (direction == -1 && D[sortedIndices[features[0]][i]] == -1)
					leftNumerator -= boostingWeight[sortedIndices[features[0]][i]];
			}
			else
			{
				rightDenominator += boostingWeight[sortedIndices[features[0]][i]];
				if (direction == 1 && D[sortedIndices[features[0]][i]] == -1)
					rightNumerator -= boostingWeight[sortedIndices[features[0]][i]];
				else if (direction == -1 && D[sortedIndices[features[0]][i]] == 1)
					rightNumerator += boostingWeight[sortedIndices[features[0]][i]];
			}
		}

		leftResponse = leftNumerator / leftDenominator;
		if (leftDenominator == 0.0 && direction == 1)
			leftResponse = 0.0000001;
		else if (leftDenominator == 0.0 && direction == -1)
			leftResponse = -0.0000001;

		rightResponse = rightNumerator / rightDenominator;
		if (rightDenominator == 0.0 && direction == 1)
			rightResponse = -0.0000001;
		else if (rightDenominator == 0.0 && direction == -1)
			rightResponse = 0.0000001;

		delete[] errors;
		delete[] directions;
		delete[] thresholds;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const double* const* X, const int* D, const int* indices, const double* boostingWeight, int samplesCount, int attributesCount, int indicesCount) override
	{
		double err;

		double* errors = new double[attributesCount];
		int* directions = new int[attributesCount];
		double* thresholds = new double[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int j = 0; j < attributesCount; j++)
		{
			int* sortedIndices = new int[indicesCount];
			memcpy(sortedIndices, indices, indicesCount * sizeof(int));
			sort_indexes(X, sortedIndices, indicesCount, j);

			auto [dir, thr] = train(X, D, sortedIndices, indicesCount, j, boostingWeight);
			directions[j] = dir;
			thresholds[j] = thr;

			double outErr = 0.0;
			for (int k = 0; k < indicesCount; k++)
			{
				int cls = classify(X[sortedIndices[k]], j, dir, thr);
				outErr += ((int)(cls != D[sortedIndices[k]])) * boostingWeight[sortedIndices[k]];
			}
			errors[j] = outErr;

			delete[] sortedIndices;
		}

		features[0] = 0;
		direction = directions[0];
		threshold = thresholds[0];
		err = errors[0];
		for (int j = 1; j < attributesCount; j++)
		{
			if (err > errors[j])
			{
				features[0] = j;
				direction = directions[j];
				threshold = thresholds[j];
				err = errors[j];
			}
		}

		double leftDenominator = 0.0, rightDenominator = 0.0, leftNumerator = 0.0, rightNumerator = 0.0;
		for (int i = 0; i < indicesCount; i++)
		{
			if (X[indices[i]][features[0] < threshold])
			{
				leftDenominator += boostingWeight[indices[i]];
				if (direction == 1 && D[indices[i]] == 1)
					leftNumerator += boostingWeight[indices[i]];
				else if (direction == -1 && D[indices[i]] == -1)
					leftNumerator -= boostingWeight[indices[i]];
			}
			else
			{
				rightDenominator += boostingWeight[indices[i]];
				if (direction == 1 && D[indices[i]] == -1)
					rightNumerator -= boostingWeight[indices[i]];
				else if (direction == -1 && D[indices[i]] == 1)
					rightNumerator += boostingWeight[indices[i]];
			}
		}

		leftResponse = leftNumerator / leftDenominator;
		if (leftDenominator == 0.0 && direction == 1)
			leftResponse = 0.0000001;
		else if (leftDenominator == 0.0 && direction == -1)
			leftResponse = -0.0000001;

		rightResponse = rightNumerator / rightDenominator;
		if (rightDenominator == 0.0 && direction == 1)
			rightResponse = -0.0000001;
		else if (rightDenominator == 0.0 && direction == -1)
			rightResponse = 0.0000001;

		delete[] errors;
		delete[] directions;
		delete[] thresholds;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy pr�bki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attributes) const override
	{
		//return this->direction * ((X[features[0]] < this->threshold) - 0.5) * 2.0;
		if (X[features[0]] < this->threshold)
			return leftResponse;
		else
			return rightResponse;
	}
};

///// <summary>Jedno poziomowe drzewo decyzyjne</summary>
//class DecisionStump : public BoostableClassifier
//{
//private:
//	int direction; /// <summary>Kierunek mniejsze/wieksze</summary>
//	double threshold; /// <summary>Prog podzialu</summary>
//	double minimum;
//	double maximum;
//
//	/// <summary>Uczenie klasyfikatora (direction, threshold)</summary>
//	/// <param name = 'X'>Probki do nauki</param>
//	/// <param name = 'D'>Klasy do nauki</param>
//	/// <param name = 'sortedIndices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
//	/// <param name = 'boostingWeight'>Wagi probek</param>
//	/// <param name = 'indicesCount'>Liczba probek</param>
//	/// <param name = 'feature'>Atrybut</param>
//	tuple<int, double> train(const double* const* X, const int* D, const int* sortedIndices, int indicesCount, int feature, const double* boostingWeight)
//	{
//		double tmpThreshold;
//		double err = INFINITY;
//
//		int dir;
//		double thr;
//
//		// poczatkowy prog dla klasyfikatora
//		thr = X[sortedIndices[0]][feature] - 0.0001;
//		double errorLt = 0, errorGt = 0;
//		for (int i = 0; i < indicesCount; i++)
//			if (D[sortedIndices[i]] == 1)
//				errorLt += boostingWeight[sortedIndices[i]];
//		errorGt = 1 - errorLt;
//
//		// Wybor miedzy znakiem mniejszosc a wiekszosci
//		if (errorLt < err)
//		{
//			err = errorLt;
//			dir = 1;
//		}
//		if (errorGt < err)
//		{
//			err = errorGt;
//			dir = -1;
//		}
//
//		// Przeszukanie kolejnych progow
//		for (int i = 0; i < indicesCount; i++)
//		{
//			// Przejscie po powtarzajacyh sie wartosciach cechy i uwzglednienie ich w bledzie
//			while (i < indicesCount - 1 && X[sortedIndices[i]][feature] == X[sortedIndices[i + 1]][feature])
//			{
//				if (D[sortedIndices[i]] == 1)
//					errorLt -= boostingWeight[sortedIndices[i]];
//				else
//					errorLt += boostingWeight[sortedIndices[i]];
//				i++;
//			}
//
//			// Wstawienie progu miedzy dwie rozne wartosci
//			if (i != indicesCount - 1)
//				tmpThreshold = (X[sortedIndices[i]][feature] + X[sortedIndices[i + 1]][feature]) / 2.0;
//			// Wstawienie progu za ostatnia probka
//			else
//				tmpThreshold = X[sortedIndices[i]][feature] + 0.0001;
//
//			// Aktualizacja bledu
//			if (D[sortedIndices[i]] == 1)
//				errorLt -= boostingWeight[sortedIndices[i]];
//			else
//				errorLt += boostingWeight[sortedIndices[i]];
//			errorGt = 1 - errorLt;
//
//			// Wybor miedzy znakiem mniejszosc a wiekszosci
//			if (errorLt < err)
//			{
//				err = errorLt;
//				dir = 1;
//				thr = tmpThreshold;
//			}
//			if (errorGt < err)
//			{
//				err = errorGt;
//				dir = -1;
//				thr = tmpThreshold;
//			}
//		}
//		return make_tuple(dir, thr);
//	}
//
//	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
//	/// <param name = 'X'>Probka do klasyfikacji</param>
//	/// <param name = 'attr'>Numer cechy</param>
//	/// <param name = 'dir'>Kierunenk nierwonosci</param>
//	/// <param name = 'thr'>Prog dla drzewa</param>
//	/// <returns>Odpowiedz klasyfikatora</returns>
//	inline int classify(const double* X, int attr, int dir, double thr) const
//	{
//		return (int)(dir * ((X[attr] < thr) - 0.5) * 2.0);
//	}
//
//public:
//	using BoostableClassifier::train;
//	using Classifier::classify;
//	using Classifier::calculateOutput;
//	using Classifier::loadModel;
//	using Classifier::saveModel;
//
//	/// <summary>Utworzenie DecisionStump-a na podstawie domyslnych parametrow</summary>
//	DecisionStump()
//	{
//		featuresCount = 1;
//		features = new int[featuresCount];
//	}
//
//	DecisionStump(ClassifierParameters &parameters)
//	{
//		featuresCount = 1;
//		features = new int[featuresCount];
//	}
//
//	/// <summary>Utworzenie DecisionStump-a na podstawie domyslnych parametrow</summary>
//	DecisionStump(DecisionStump* toCopy)
//	{
//		this->direction = toCopy->direction;
//		this->threshold = toCopy->threshold;
//		this->minimum = toCopy->minimum;
//		this->maximum = toCopy->maximum;
//		this->featuresCount = toCopy->featuresCount;
//		this->features = new int[featuresCount];
//		this->features[0] = toCopy->features[0];
//	}
//
//	/// <summary>Zaladowanie DecisionStump-a  z pliku o podanej sciezce</summary>
//	/// <param name = 'path'>Sciezka do pliku</param>
//	DecisionStump(string path)
//	{
//		featuresCount = 1;
//		features = new int[featuresCount];
//
//		loadModel(path);
//	}
//
//	/// <summary>Zaladowanie DecisionStump-a z podanego strumienia</summary>
//	/// <param name = 'input'>Strumien do pliku</param>
//	DecisionStump(ifstream &input)
//	{
//		featuresCount = 1;
//		features = new int[featuresCount];
//
//		loadModel(input);
//	}
//
//	DecisionStump(ifstream &input, ClassifierParameters &parameters)
//	{
//		featuresCount = 1;
//		features = new int[featuresCount];
//
//		loadModel(input, parameters);
//	}
//
//	/// <summary>Zwraca typ klasyfikatora</summary>
//	/// <returns>Typ klasyfikatora</returns>
//	static string GetType()
//	{
//		return "DecisionStump";
//	}
//
//	/// <summary>Zwraca typ klasyfikatora</summary>
//	/// <returns>Typ klasyfikatora</returns>
//	string getType() const override
//	{
//		return GetType();
//	}
//
//	/// <summary>Zwraca opis klasyfikatora</summary>
//	/// <returns>Opis klasyfikatora</returns>
//	string toString() const override
//	{
//		string text = getType() + "\r\n";
//		text += "Feature Count: " + to_string(featuresCount) + "\r\n";
//		text += "Feature: " + to_string(features[0]) + "\r\n";
//		text += "Direction: " + to_string(direction) + "\r\n";
//		text += "Threshold: " + to_string(threshold) + "\r\n";
//		text += "Minimum: " + to_string(minimum) + "\r\n";
//		text += "Maximum: " + to_string(maximum) + "\r\n";
//
//		return text;
//	}
//
//	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
//	/// <param name = 'input'>Strumien do pliku</param>
//	void loadModel(ifstream &input) override
//	{
//		string fieldName, type;
//
//		skipHeader(input);
//
//		input >> fieldName >> type;
//		if (type == getType())
//		{
//			double fileVer;
//			input >> fieldName >> fileVer;
//
//			skipHeader(input);
//			input >> fieldName >> features[0];
//			input >> fieldName >> direction;
//			input >> fieldName >> threshold;
//			input >> fieldName >> minimum;
//			input >> fieldName >> maximum;
//		}
//		else
//			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
//	}
//
//	void loadModel(ifstream &input, ClassifierParameters &parameters) override
//	{
//		loadModel(input);
//	}
//
//	/// <summary>Zapisanie modelu do podanego strumienia</summary>
//	/// <param name = 'output'>Strumien do pliku</param>
//	void saveModel(ofstream &output) const override
//	{
//		createMainHeader(output, "Classifier_Info:");
//		output << "Type: " << getType() << endl;
//		output << "Save_Format: 2.0" << endl;
//		createSecondaryHeader(output, "Model:");
//		output << "Feature: " << features[0] << endl;
//		output << "Direction: " << direction << endl;
//		output << "Threshold: " << threshold << endl;
//		output << "Minimum: " << minimum << endl;
//		output << "Maximum: " << maximum << endl;
//	}
//
//	/// <summary>Uczenie klasyfikatora</summary>
//	/// <param name = 'X'>Probki do nauki</param>
//	/// <param name = 'D'>Klasy do nauki</param>
//	/// <param name = 'sortedIndices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
//	/// <param name = 'boostingWeight'>Wagi probek</param>
//	/// <param name = 'samples'>Liczba probek</param>
//	/// <param name = 'attributes'>Liczba atrybutow</param>
//	void train(const double* const* X, const int* D, const int* const* sortedIndices, const double* boostingWeight, int samplesCount, int attributesCount, int indicesCount)
//	{
//		double err;
//
//		double* errors = new double[attributesCount];
//		int* directions = new int[attributesCount];
//		double* thresholds = new double[attributesCount];
//
//#pragma omp parallel for num_threads(OMP_NUM_THR)
//		for (int j = 0; j < attributesCount; j++)
//		{
//			auto[dir, thr] = train(X, D, sortedIndices[j], indicesCount, j, boostingWeight);
//			directions[j] = dir;
//			thresholds[j] = thr;
//
//			double outErr = 0.0;
//			for (int k = 0; k < indicesCount; k++)
//			{
//				int cls = classify(X[sortedIndices[j][k]], j, dir, thr);
//				outErr += (int)(cls != D[sortedIndices[j][k]]) * boostingWeight[sortedIndices[j][k]];
//			}
//			errors[j] = outErr;
//		}
//
//		features[0] = 0;
//		direction = directions[0];
//		threshold = thresholds[0];
//		err = errors[0];
//		for (int j = 1; j < attributesCount; j++)
//		{
//			if (err > errors[j])
//			{
//				features[0] = j;
//				direction = directions[j];
//				threshold = thresholds[j];
//				err = errors[j];
//			}
//		}
//
//		minimum = X[sortedIndices[features[0]][0]][features[0]];
//		maximum = X[sortedIndices[features[0]][indicesCount - 1]][features[0]];
//
//		delete[] errors;
//		delete[] directions;
//		delete[] thresholds;
//	}
//
//	/// <summary>Uczenie klasyfikatora</summary>
//	/// <param name = 'X'>Probki do nauki</param>
//	/// <param name = 'D'>Klasy do nauki</param>
//	/// <param name = 'samples'>Liczba probek</param>
//	/// <param name = 'attributes'>Liczba atrybutow</param>
//	void train(const double* const* X, const int* D, const int* indices, const double* boostingWeight, int samplesCount, int attributesCount, int indicesCount) override
//	{
//		double err;
//
//		double* errors = new double[attributesCount];
//		int* directions = new int[attributesCount];
//		double* thresholds = new double[attributesCount];
//		double* minimas = new double[attributesCount];
//		double* maximas = new double[attributesCount];
//
//#pragma omp parallel for num_threads(OMP_NUM_THR)
//		for (int j = 0; j < attributesCount; j++)
//		{
//			int* sortedIndices = new int[indicesCount];
//			memcpy(sortedIndices, indices, indicesCount * sizeof(int));
//			sort_indexes(X, sortedIndices, indicesCount, j);
//
//			auto[dir, thr] = train(X, D, sortedIndices, indicesCount, j, boostingWeight);
//			directions[j] = dir;
//			thresholds[j] = thr;
//			minimas[j] = X[sortedIndices[0]][j];
//			maximas[j] = X[sortedIndices[indicesCount - 1]][j];
//
//			double outErr = 0.0;
//			for (int k = 0; k < indicesCount; k++)
//			{
//				int cls = classify(X[sortedIndices[k]], j, dir, thr);
//				outErr += ((int)(cls != D[sortedIndices[k]])) * boostingWeight[sortedIndices[k]];
//			}
//			errors[j] = outErr;
//
//			delete[] sortedIndices;
//		}
//
//		features[0] = 0;
//		direction = directions[0];
//		threshold = thresholds[0];
//		err = errors[0];
//		for (int j = 1; j < attributesCount; j++)
//		{
//			if (err > errors[j])
//			{
//				features[0] = j;
//				direction = directions[j];
//				threshold = thresholds[j];
//				err = errors[j];
//			}
//		}
//
//		minimum = minimas[features[0]];
//		maximum = maximas[features[0]];
//
//		delete[] errors;
//		delete[] directions;
//		delete[] thresholds;
//		delete[] minimas;
//		delete[] maximas;
//	}
//
//	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
//	/// <param name = 'X'>Cechy pr�bki do klasyfikacji</param>
//	/// <returns>Odpowiedz klasyfikatora</returns>
//	inline double calculateOutput(const double* X, int attributes) const override
//	{
//		double x = X[features[0]];
//		//return this->direction * ((X[features[0]] < this->threshold) - 0.5) * 2.0;
//		if (x < this->threshold)
//		{
//			if (this->minimum - this->threshold != 0.0)
//				return this->direction * (x - this->threshold) / (this->minimum - this->threshold);
//			else
//				return this->direction;
//		}
//		else
//		{
//			if (this->threshold - this->maximum != 0.0)
//				return this->direction * (x - this->threshold) / (this->threshold - this->maximum);
//			else
//				return -this->direction;
//		}
//	}
//};

/// <summary>Perceptron prosty bazujacy na jednej cesze</summary>
class WeakPerceptron : public BoostableClassifier
{
private:
	double w[2] = { 0.0, 0.0 }; /// <summary>Wagi perceptronu</summary> 
	int maxIT; /// <summary>Maksymalna liczba iteracji</summary>
	double eta; /// <summary>Wspolczynnik uczenia</summary>

	 /// <summary>Uczenie klasyfikatora (bias, weight)</summary>
	 /// <param name = 'X'>Probki do nauki</param>
	 /// <param name = 'D'>Klasy do nauki</param>
	 /// <param name = 'indices'>Macierz okreslajaca kolejnosc dostepu do probek</param>
	 /// <param name = 'boostingWeight'>Wagi probek</param>
	 /// <param name = 'samples'>Liczba probek</param>
	 /// <param name = 'feature'>Atrybut</param>
	tuple<double, double> train(const double* const* X, const int* D, const int* indices, int feature, const double* boostingWeight, int indicesCount)
	{
		double bias = 0.0;
		double weight = 0.0;

		int incorect;
		for (int it = 0; it < maxIT; it++)
		{
			incorect = 0;

			double x;
			int y, f;
			double s, er, tmp;
			for (int i = 0; i < indicesCount; i++)
			{
				x = X[indices[i]][feature];
				y = D[indices[i]];
				s = weight * x + bias;

				// obliczanie bledu dla probki
				f = -1;
				if (s >= 0)
					f = 1;
				er = (y - f) / 2.0;

				if (er != 0)
					incorect += 1;

				// wyznaczenie poprawki wag (regula delta)
				tmp = eta * er * (boostingWeight[indices[i]] * indicesCount);
				bias += 1 * tmp;
				weight += x * tmp;
			}
			if (incorect == 0)
				break;
		}

		return make_tuple(bias, weight);
	}

	inline int classify(const double* X, int attr, double b, double weight) const
	{
		if ((weight * X[attr] + b) >= 0)
			return 1;
		else
			return -1;
	}

public:
	using BoostableClassifier::train;
	using Classifier::calculateOutput;
	using Classifier::classify;
	using Classifier::loadModel;
	using Classifier::saveModel;

	~WeakPerceptron() override {}

	/// <summary>Utworzenie Perceptronu na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	WeakPerceptron(const ClassifierParameters& parameters)
	{
		this->maxIT = parameters.maxIterations;
		this->eta = parameters.learningRate;

		featuresCount = 1;
		features = new int[featuresCount];
	}

	/// <summary>Utworzenie Perceptronu na podstawie podanych parametrow</summary>
	/// <param name = 'maxIT'>Liczba iteracji</param>
	/// <param name = 'eta'>Wspolczynnik uczenia</param>
	WeakPerceptron(int maxIT, double eta)
	{
		this->maxIT = maxIT;
		this->eta = eta;

		featuresCount = 1;
		features = new int[featuresCount];
	}

	WeakPerceptron()
	{
		this->maxIT = 150;
		this->eta = 0.1;

		featuresCount = 1;
		features = new int[featuresCount];
	}

	/// <summary>Utworzenie Perceptronu na podstawie podanych parametrow</summary>
	WeakPerceptron(WeakPerceptron* toCopy)
	{
		this->maxIT = toCopy->maxIT;
		this->eta = toCopy->eta;
		this->w[0] = toCopy->w[0];
		this->w[1] = toCopy->w[1];

		this->featuresCount = toCopy->featuresCount;
		this->features = new int[featuresCount];
		this->features[0] = toCopy->features[0];
	}

	/// <summary>Zaladowanie Perceptronu z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	WeakPerceptron(string path)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(path);
	}

	/// <summary>Zaladowanie Perceptronu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	WeakPerceptron(ifstream& input)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(input);
	}

	/// <summary>Zaladowanie Perceptronu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	WeakPerceptron(ifstream& input, ClassifierParameters& parameter)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(input, parameter);
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "WeakPerceptron";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Feature Count: " + to_string(featuresCount) + "\r\n";
		text += "Feature: " + to_string(features[0]) + "\r\n";
		text += "Weight: " + to_string(w[0]) + " " + to_string(w[1]) + "\r\n";

		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		string fieldName, type;

		skipHeader(input);

		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> features[0];
			input >> fieldName >> w[0];
			input >> fieldName >> w[1];

			skipHeader(input);
			input >> fieldName >> maxIT;
			input >> fieldName >> eta;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
	}

	void loadModel(ifstream& input, ClassifierParameters& parameters) override
	{
		loadModel(input);

		parameters.maxIterations = maxIT;
		parameters.learningRate = eta;
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Feature: " << features[0] << endl;
		output << "Bias: " << w[0] << endl;
		output << "Weight: " << w[1] << endl;
		createSecondaryHeader(output, "Training_Parameters:");
		output << "Max_Iterations: " << maxIT << endl;
		output << "Learning_Factor: " << eta << endl;
	}

	void saveModelOld(ofstream& output) const override
	{
		output << getType() << endl;
		output << features[0] << endl;
		output << w[0] << " " << w[1] << endl;
		output << maxIT << endl;
		output << eta << endl;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'X'>Probki do nauki</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const double* const* X, const int* D, const int* indices, const double* boostingWeights, int samplesCount, int attributesCount, int indicesCount) override
	{
		double err;

		double* errors = new double[attributesCount];
		double* biases = new double[attributesCount];
		double* weights = new double[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int j = 0; j < attributesCount; j++)
		{
			auto [b, weight] = train(X, D, indices, j, boostingWeights, indicesCount);

			// testowanie nauczonego modelu
			double outErr = 0.0;
			for (int k = 0; k < indicesCount; k++)
			{
				int cls = classify(X[indices[k]], j, b, weight);
				outErr += (int)(cls != D[indices[k]]) * boostingWeights[indices[k]];
			}
			errors[j] = outErr;
			biases[j] = b;
			weights[j] = weight;
		}

		features[0] = 0;
		w[0] = biases[0];
		w[1] = weights[0];
		err = errors[0];
		for (int j = 1; j < attributesCount; j++)
		{
			if (err > errors[j])
			{
				features[0] = j;
				w[0] = biases[j];
				w[1] = weights[j];
				err = errors[j];
			}
		}

		delete[] errors;
		delete[] biases;
		delete[] weights;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy pr�bki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attributes) const override
	{
		return (w[1] * X[features[0]] + w[0]);
	}
};

/// <summary>Regularnie rozlozone kosze z logitami</summary>
class RegularBins : public BoostableBinnedClassifier
{
private:
	double minimum = INFINITY; /// <summary>Minimum dla wybranej cechy</summary>
	double maximum = -INFINITY; /// <summary>Maksimum dla wybranej cechy</summary>
	double* responses = nullptr; /// <summary>Odpowiedz klasyfikatora dla kazdego z koszy</summary>

	inline int classify(const int* xInBin, int attr, const double* responses)
	{
		double output = responses[xInBin[attr]];
		return (int)((output >= 0.0) * 2 - 1);
	}

	void train(const int* const* xInBin, const double* xRange, const int* D, const int* indices, int indicesCount, int feature, const double* weightAda, double* responses)
	{
		minimum = xRange[0];
		maximum = xRange[1];

		vector<double> negativesWeightsSumInBins(B);
		vector<double> positivesWeightsSumInBins(B);
		for (int i = 0; i < indicesCount; i++)
		{
			if (D[indices[i]] == 1)
				positivesWeightsSumInBins[xInBin[indices[i]][feature]] += weightAda[indices[i]];
			else if (D[indices[i]] == -1)
				negativesWeightsSumInBins[xInBin[indices[i]][feature]] += weightAda[indices[i]];
		}

		for (int k = 0; k < B; k++)
		{
			double probabilityQuotient = positivesWeightsSumInBins[k] / negativesWeightsSumInBins[k];
			if (isnan(probabilityQuotient))
				responses[k] = 0.0;
			else if (probabilityQuotient < exp(-4))
				responses[k] = -2.0;
			else if (probabilityQuotient > exp(4))
				responses[k] = 2.0;
			else
				responses[k] = 0.5 * log(probabilityQuotient);
		}
	}

public:
	using BoostableBinnedClassifier::train;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::loadModel;
	using Classifier::saveModel;

	~RegularBins()
	{
		delete[] responses;
	}

	RegularBins()
	{
		this->B = 8;
		this->outlayerPercent = 0;

		featuresCount = 1;
		features = new int[featuresCount];
		responses = new double[B];
	}

	/// <summary>Utworzenie Perceptronu na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	RegularBins(const ClassifierParameters& parameters)
	{
		this->B = parameters.treeBins;
		this->outlayerPercent = parameters.outlayerPercent;

		featuresCount = 1;
		features = new int[featuresCount];
		responses = new double[B];
	}

	/// <summary>Utworzenie Perceptronu na podstawie podanych parametrow</summary>
	/// <param name = 'bins'>Liczba koszy</param>
	RegularBins(int bins)
	{
		this->B = bins;
		this->outlayerPercent = 0;

		featuresCount = 1;
		features = new int[featuresCount];
		responses = new double[B];
	}

	/// <summary>Utworzenie Perceptronu na podstawie podanych parametrow</summary>
	/// <param name = 'bins'>Liczba koszy</param>
	/// <param name = 'bins'>Procent probek odstajacych</param>
	RegularBins(int bins, double outlayers)
	{
		this->B = bins;
		this->outlayerPercent = outlayers;

		featuresCount = 1;
		features = new int[featuresCount];
		responses = new double[B];
	}

	RegularBins(RegularBins* toCopy)
	{
		this->B = toCopy->B;
		this->outlayerPercent = toCopy->outlayerPercent;
		this->minimum = toCopy->minimum;
		this->maximum = toCopy->maximum;

		this->featuresCount = toCopy->featuresCount;
		this->features = new int[featuresCount];
		this->features[0] = toCopy->features[0];
		this->responses = new double[B];
		for (int i = 0; i < B; i++)
			responses[i] = toCopy->responses[i];
	}

	/// <summary>Zaladowanie Perceptronu z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	RegularBins(string path)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(path);
	}

	/// <summary>Zaladowanie Perceptronu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	RegularBins(ifstream& input, ClassifierParameters& parameter)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(input, parameter);
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "RegularBins";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Bins: " + to_string(B) + "\r\n";
		text += "Feature: " + to_string(features[0]) + "\r\n";
		text += "Minimum: " + to_string(minimum) + "\r\n";
		text += "Maximum: " + to_string(maximum) + "\r\n";
		text += "Responses: \r\n";
		for (int i = 0; i < B; i++)
			text += to_string(i) + ": " + to_string(responses[i]) + "\r\n";

		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		string fieldName, type;

		skipHeader(input);

		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> features[0];
			input >> fieldName >> B;
			input >> fieldName >> minimum;
			input >> fieldName >> maximum;

			input >> fieldName;
			if (responses != nullptr)
				delete[] responses;
			responses = new double[B];
			for (int i = 0; i < B; i++)
				input >> responses[i];

			skipHeader(input);
			input >> fieldName >> outlayerPercent;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
	}

	void loadModel(ifstream& input, ClassifierParameters& parameters) override
	{
		loadModel(input);

		parameters.outlayerPercent = outlayerPercent;
		parameters.treeBins = B;
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Feature: " << features[0] << endl;
		output << "Bins: " << B << endl;
		output << "Minimum: " << minimum << endl;
		output << "Maximum: " << maximum << endl;
		output << "Responses:" << endl;
		for (int i = 0; i < B; i++)
			output << responses[i] << endl;
		createSecondaryHeader(output, "Training_Parameters:");
		output << "Outlayers_Percentage: " << outlayerPercent << endl;
	}

	void saveModelOld(ofstream& output) const override
	{
		output << getType() << endl;
		output << B << endl;
		output << features[0] << endl;
		output << minimum << endl;
		output << maximum << endl;
		for (int i = 0; i < B; i++)
			output << responses[i] << endl;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'xInBin'>Numer kosza, do ktorego nalezy dana wartosc</param>
	/// <param name = 'xRanges'>Zakres cech</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'boostingWeights'>Wagi dla boostingu</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const int* const* xInBin, const double* const* xRanges, const int* D, const int* indices, const double* boostingWeights, int samplesCount, int attributesCount, int indicesCount) override
	{
		double err;

		double* errors = new double[attributesCount];
		double** responsesMatrix = new double* [attributesCount];
		for (int i = 0; i < attributesCount; i++)
			responsesMatrix[i] = new double[B];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int j = 0; j < attributesCount; j++)
		{
			train(xInBin, xRanges[j], D, indices, indicesCount, j, boostingWeights, responsesMatrix[j]);

			// testowanie nauczonego modelu
			double outErr = 0.0;
			for (int k = 0; k < indicesCount; k++)
			{
				int cls = classify(xInBin[indices[k]], j, responsesMatrix[j]);
				outErr += (int)(cls != D[indices[k]]) * boostingWeights[indices[k]];
			}
			errors[j] = outErr;
		}

		features[0] = 0;
		minimum = xRanges[0][0];
		maximum = xRanges[0][1];
		for (int i = 0; i < B; i++)
			responses[i] = responsesMatrix[0][i];
		err = errors[0];
		for (int j = 1; j < attributesCount; j++)
		{
			if (err > errors[j])
			{
				features[0] = j;
				minimum = xRanges[j][0];
				maximum = xRanges[j][1];
				for (int i = 0; i < B; i++)
					responses[i] = responsesMatrix[j][i];
				err = errors[j];
			}
		}

		delete[] errors;
		for (int i = 0; i < attributesCount; i++)
			delete[] responsesMatrix[i];
		delete[] responsesMatrix;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
	/// <param name = 'X'>Cechy pr�bki do klasyfikacji</param>
	/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attributes) const override
	{
		double x = (X[features[0]] - minimum) / (maximum - minimum);
		double output = responses[isInBin(x, B)];
		return output;
	}
};

/// <summary>Decison stump z przedzialami</summary>
class BinnedDecisionStump : public BoostableBinnedClassifier
{
private:
	double threshold = 0.0; /// <summary>Prog podzialu</summary>
	double responseLeft = 0.0;
	double responseRight = 0.0;

	inline int classify(const int* xInBin, int attr, const double responseLeft, const double responseRight, const int thresholdBin)
	{
		double output = responseRight;
		if (xInBin[attr] < thresholdBin)
			output = responseLeft;
		return output >= 0.0 ? 1 : -1;
	}

	/// <summary>Nauka klasyfikatora dla wskazanej cechy (responseLeft, responseRight, threshold, thresholdBin)</summary>
	tuple<double, double, double, int> train(const int* const* xInBin, const double* xRange, const int* D, const int* indices, int indicesCount, int feature, const double* weightAda)
	{
		double minimum = xRange[0];
		double maximum = xRange[1];

		double finalLeftResponse;
		double finalRightResponse;
		double finalThreshold;

		vector<double> negativesWeightsSumInBins(B);
		vector<double> positivesWeightsSumInBins(B);
		for (int i = 0; i < indicesCount; i++)
		{
			if (D[indices[i]] == 1)
				positivesWeightsSumInBins[xInBin[indices[i]][feature]] += weightAda[indices[i]];
			else if (D[indices[i]] == -1)
				negativesWeightsSumInBins[xInBin[indices[i]][feature]] += weightAda[indices[i]];
		}

		double krok = (maximum - minimum) / B;
		double thr = minimum + krok;
		int thresholdBinTmp = 1;
		int thresholdBin = 1;

		double positivesWeightsLeftSum = positivesWeightsSumInBins[0];
		double negativesWeightsLeftSum = negativesWeightsSumInBins[0];
		double positivesWeightsRightSum = 0, negativesWeightsRightSum = 0;
		for (int b = 1; b < B; b++)
		{
			positivesWeightsRightSum += positivesWeightsSumInBins[b];
			negativesWeightsRightSum += negativesWeightsSumInBins[b];
		}

		double responseLeft;
		double responseRight;

		double probabilityQuotient = positivesWeightsLeftSum / negativesWeightsLeftSum;
		if (isnan(probabilityQuotient))
			responseLeft = 0.0;
		else if (probabilityQuotient < 1 / exp(4))
			responseLeft = -2.0;
		else if (probabilityQuotient > exp(4))
			responseLeft = 2.0;
		else
			responseLeft = 0.5 * log(probabilityQuotient);

		probabilityQuotient = positivesWeightsRightSum / negativesWeightsRightSum;
		if (isnan(probabilityQuotient))
			responseRight = 0.0;
		else if (probabilityQuotient < 1 / exp(4))
			responseRight = -2.0;
		else if (probabilityQuotient > exp(4))
			responseRight = 2.0;
		else
			responseRight = 0.5 * log(probabilityQuotient);

		double errTmp = 0.0;
		responseLeft >= 0.0 ? errTmp += negativesWeightsLeftSum : errTmp += positivesWeightsLeftSum;
		responseRight >= 0.0 ? errTmp += negativesWeightsRightSum : errTmp += positivesWeightsRightSum;

		double err = errTmp;
		finalThreshold = thr;
		finalLeftResponse = responseLeft;
		finalRightResponse = responseRight;

		// sprawdzenie kolejnych progow
		for (int b = 1; b < B; b++)
		{
			thr += krok;
			thresholdBinTmp += 1;

			// aktualizacja licznosci
			positivesWeightsLeftSum += positivesWeightsSumInBins[b];
			positivesWeightsRightSum -= positivesWeightsSumInBins[b];
			negativesWeightsLeftSum += negativesWeightsSumInBins[b];
			negativesWeightsRightSum -= negativesWeightsSumInBins[b];

			probabilityQuotient = positivesWeightsLeftSum / negativesWeightsLeftSum;
			if (isnan(probabilityQuotient))
				responseLeft = 0.0;
			else if (probabilityQuotient < exp(-4))
				responseLeft = -2.0;
			else if (probabilityQuotient > exp(4))
				responseLeft = 2.0;
			else
				responseLeft = 0.5 * log(probabilityQuotient);

			probabilityQuotient = positivesWeightsRightSum / negativesWeightsRightSum;
			if (isnan(probabilityQuotient))
				responseRight = 0.0;
			else if (probabilityQuotient < 1 / exp(4))
				responseRight = -2.0;
			else if (probabilityQuotient > exp(4))
				responseRight = 2.0;
			else
				responseRight = 0.5 * log(probabilityQuotient);

			errTmp = 0.0;
			responseLeft >= 0 ? errTmp += negativesWeightsLeftSum : errTmp += positivesWeightsLeftSum;
			responseRight >= 0 ? errTmp += negativesWeightsRightSum : errTmp += positivesWeightsRightSum;

			// zaktualizowanie danych wezla
			if (errTmp < err)
			{
				err = errTmp;
				finalThreshold = thr;
				finalLeftResponse = responseLeft;
				finalRightResponse = responseRight;
				thresholdBin = thresholdBinTmp;
			}
		}

		return make_tuple(finalLeftResponse, finalRightResponse, finalThreshold, thresholdBin);
	}

public:
	using BoostableBinnedClassifier::train;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::loadModel;
	using Classifier::saveModel;

	/// <summary>Utworzenie Perceptronu na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	BinnedDecisionStump(const ClassifierParameters& parameters)
	{
		this->B = parameters.treeBins;
		this->outlayerPercent = parameters.outlayerPercent;

		featuresCount = 1;
		features = new int[featuresCount];
	}

	BinnedDecisionStump(BinnedDecisionStump* toCopy)
	{
		this->B = toCopy->B;
		this->outlayerPercent = toCopy->outlayerPercent;
		this->threshold = toCopy->threshold;
		this->responseLeft = toCopy->responseLeft;
		this->responseRight = toCopy->responseRight;

		this->featuresCount = toCopy->featuresCount;
		this->features = new int[featuresCount];
		this->features[0] = toCopy->features[0];
	}

	/// <summary>Utworzenie Perceptronu na podstawie podanych parametrow</summary>
	BinnedDecisionStump()
	{
		this->B = 16;
		this->outlayerPercent = 0;

		featuresCount = 1;
		features = new int[featuresCount];
	}

	/// <summary>Utworzenie Perceptronu na podstawie podanych parametrow</summary>
	/// <param name = 'bins'>Liczba koszy</param>
	BinnedDecisionStump(int bins)
	{
		this->B = bins;
		this->outlayerPercent = 0;

		featuresCount = 1;
		features = new int[featuresCount];
	}

	/// <summary>Utworzenie Perceptronu na podstawie podanych parametrow</summary>
	/// <param name = 'bins'>Liczba koszy</param>
	BinnedDecisionStump(int bins, double outlayers)
	{
		this->B = bins;
		this->outlayerPercent = outlayers;

		featuresCount = 1;
		features = new int[featuresCount];
	}

	/// <summary>Zaladowanie Perceptronu z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	BinnedDecisionStump(string path)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(path);
	}

	/// <summary>Zaladowanie Perceptronu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	BinnedDecisionStump(ifstream& input)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(input);
	}

	BinnedDecisionStump(ifstream& input, ClassifierParameters& parameter)
	{
		featuresCount = 1;
		features = new int[featuresCount];

		loadModel(input, parameter);
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "BinnedDecisionStump";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Bins: " + to_string(B) + "\r\n";
		text += "Feature: " + to_string(features[0]) + "\r\n";
		text += "Response Left: " + to_string(responseLeft) + "\r\n";
		text += "Response Right: " + to_string(responseRight) + "\r\n";
		text += "Threshold: " + to_string(threshold) + "\r\n";

		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		string fieldName, type;

		skipHeader(input);

		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> features[0];
			input >> fieldName >> B;
			input >> fieldName >> threshold;
			input >> fieldName >> responseLeft;
			input >> fieldName >> responseRight;

			skipHeader(input);
			input >> fieldName >> outlayerPercent;
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
	}

	void loadModel(ifstream& input, ClassifierParameters& parameters) override
	{
		loadModel(input);

		parameters.outlayerPercent = outlayerPercent;
		parameters.treeBins = B;
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Feature: " << features[0] << endl;
		output << "Bins: " << B << endl;
		output << "Threshold: " << threshold << endl;
		output << "Response_Left: " << responseLeft << endl;
		output << "Response_Right: " << responseRight << endl;
		createSecondaryHeader(output, "Training_Parameters:");
		output << "Outlayers_Percentage: " << outlayerPercent << endl;
	}

	void saveModelOld(ofstream& output) const override
	{
		output << getType() << endl;
		output << B << endl;
		output << features[0] << endl;
		output << threshold << endl;
		output << responseLeft << endl;
		output << responseRight << endl;
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'xInBin'>Numer kosza, do ktorego nalezy dana wartosc</param>
	/// <param name = 'xRanges'>Zakres cech</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'boostingWeights'>Wagi dla boostingu</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const int* const* xInBin, const double* const* xRanges, const int* D, const int* indices, const double* boostingWeights, int samplesCount, int attributesCount, int indicesCount) override
	{
		double err;

		double* errors = new double[attributesCount];
		double* responsesLeft = new double[attributesCount];
		double* responsesRight = new double[attributesCount];
		double* thresholds = new double[attributesCount];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int j = 0; j < attributesCount; j++)
		{
			auto [resLeft, resRight, thr, thrBin] = train(xInBin, xRanges[j], D, indices, indicesCount, j, boostingWeights);

			// testowanie nauczonego modelu
			double outErr = 0.0;
			for (int k = 0; k < indicesCount; k++)
			{
				int cls = classify(xInBin[indices[k]], j, resLeft, resRight, thrBin);
				outErr += (int)(cls != D[indices[k]]) * boostingWeights[indices[k]];
			}
			errors[j] = outErr;

			responsesLeft[j] = resLeft;
			responsesRight[j] = resRight;
			thresholds[j] = thr;
		}

		features[0] = 0;
		responseLeft = responsesLeft[0];
		responseRight = responsesRight[0];
		threshold = thresholds[0];
		err = errors[0];
		for (int j = 1; j < attributesCount; j++)
		{
			if (err > errors[j])
			{
				features[0] = j;
				responseLeft = responsesLeft[j];
				responseRight = responsesRight[j];
				threshold = thresholds[j];
				err = errors[j];
			}
		}

		delete[] errors;
		delete[] responsesLeft;
		delete[] responsesRight;
		delete[] thresholds;
	}

	inline double calculateOutput(const double* X, int attributes) const override
	{
		double output = responseRight;
		if (X[features[0]] <= this->threshold)
			output = responseLeft;
		return output;
	}
};

/// <summary>Rozszerzenie decision stump o wielo poziomowosc</summary>
class BinnedTree : public BoostableBinnedClassifier
{
private:
	/// <summary>Klasa reperezentujaca wezel drzewa</summary>
	class Node
	{
	public:
		Node* Left = nullptr; /// <summary>Lewe dziecko danego wezla</summary>
		Node* Right = nullptr; /// <summary>Prawe dziecko danego wezla</summary>

		const string id;
		const int level; /// <summary>Poziom w drzewie</summary>	
		const string type; /// <summary>Typ wezla, korzen, prawe/lewe dziecko</summary>

		int feature; /// <summary>Cecha na, ktorej bazuje</summary>
		double threshold; /// <summary>Prog dla danego wezla</summary>
		double responseLeft = 0; /// <summary>Zwracana warto�� je�li atrybut jest mniejszy od progu </summary>
		double responseRight = 0; /// <summary>Zwracana warto�� je�li atrybut jest wiekszy od progu</summary>

		~Node()
		{
			if (Left != nullptr)
				delete Left;
			if (Right != nullptr)
				delete Right;
		}

		/// <summary>Utworzenie wezla na podstawie podanych parametrow</summary>
		/// <param name = 'type'>Typ wezla (LEFT, RIGHT, ROOT)</param>
		/// <param name = 'level'>Poziom w drzewie</param>
		/// <param name = 'feature'>Cecha na, ktorej bazuje</param>
		Node() : type("ROOT"), level(0), id("0") {}

		Node(string type, int level, string id) : type(type), level(level), id(id) {}

		/// <summary>Utworzenie wezla na podstawie innego wezla</summary>
		/// <param name = 'node'>Wezel bazowy</param>
		Node(Node* node) : type(node->type), level(node->level), id(node->id)
		{
			this->threshold = node->threshold;
			this->feature = node->feature;
			this->responseLeft = node->responseLeft;
			this->responseRight = node->responseRight;

			if (node->Left != nullptr)
				Left = new Node(node->Left);
			else
				Left = nullptr;

			if (node->Right != nullptr)
				Right = new Node(node->Right);
			else
				Right = nullptr;
		}

		/// <summary>Zaladowanie wezla z podanego strumienia</summary>
		/// <param name = 'input'>Strumien do pliku</param>
		static Node* loadNode(ifstream& input)
		{
			int level;
			string id, type;
			bool child;

			string fieldName;
			Classifier::skipHeader(input);

			input >> fieldName >> type;
			input >> fieldName >> level;
			input >> fieldName >> id;
			if (level == 0 && type != "ROOT")
				throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
			Node* node = new Node(type, level, id);

			Classifier::skipHeader(input);
			input >> fieldName >> node->threshold;
			input >> fieldName >> node->feature;
			input >> fieldName >> node->responseLeft;
			input >> fieldName >> node->responseRight;

			Classifier::skipHeader(input);
			input >> fieldName >> child;

			if (child)
				node->Left = loadNode(input);

			input >> fieldName >> child;
			if (child)
				node->Right = loadNode(input);

			Classifier::skipHeader(input);

			return node;
		}

		/// <summary>Zapisanie modelu do podanego strumienia</summary>
		/// <param name = 'output'>Strumien do pliku</param>
		void saveNode(ofstream& output) const
		{
			Classifier::createMainHeader(output, "Node:");
			output << "Type: " << type << endl;
			output << "Level: " << level << endl;
			output << "Id: " << id << endl;

			Classifier::createSecondaryHeader(output, "Model:");
			output << "Threshold: " << threshold << endl;
			output << "Feature: " << feature << endl;
			output << "Response_Left: " << responseLeft << endl;
			output << "Response_Right: " << responseRight << endl;

			Classifier::createSecondaryHeader(output, "Childs:");
			if (Left != nullptr)
			{
				output << "Have_Left_Child: " << true << endl;
				Left->saveNode(output);
			}
			else
				output << "Have_Left_Child: " << false << endl;

			if (Right != nullptr)
			{
				output << "Have_Right_Child: " << true << endl;
				Right->saveNode(output);
			}
			else
				output << "Have_Right_Child: " << false << endl;
			Classifier::createMainHeader(output, "End_Node:");
		}

		void saveNodeOld(ofstream& output) const
		{
			output << type << endl;
			output << level << endl;
			output << threshold << endl;
			output << feature << endl;
			output << responseLeft << endl;
			output << responseRight << endl;

			if (Left != nullptr)
				Left->saveNode(output);
			else
				output << "NULL_LEFT" << endl;

			if (Right != nullptr)
				Right->saveNode(output);
			else
				output << "NULL_RIGHT" << endl;
		}

		/// <summary>Uczenie wezla</summary>
		/// <param name = 'xInBin'>Numery koszy dla pr�bek ucz�cych</param>
		/// <param name = 'xRanges'>Zakresy atrybutow</param>
		/// <param name = 'D'>Klasy pr�bek ucz�cych</param>
		/// <param name = 'samplesIndices'>Indeksy probek bioracych udzial w nauce</param>
		/// <param name = 'noBins'>Liczba koszy</param>
		/// <param name = 'maxLevel'>Maksymalny poziom drzewa</param>
		/// <param name = 'weightAda'>Wagi probek</param>
		/// <param name = 'impurityMetric'>Miara zanieczyszczen</param>

		void train(const int* const* xInBin, const double* const* xRanges, const int* D, const int indicesCount, const int attributesCount, const int* samplesIndices, const int noBins, int maxLevel, const double* weightAda, string impurityMetric)
		{
			double(*information)(double, double, double, double);
			if (impurityMetric == "Gini")
				information = GiniIndex;
			else if (impurityMetric == "Entrophy")
				information = InformationGain;
			else
				throw ERRORS::INCORRECT_METRICES;

			int savedThresholdBinNumber = 0;
			double savedPositivesProbabilityRight = 0;
			double savedPositivesProbabilityLeft = 0;

			// przypisanie koszy oraz zbudowanie statystyk dla kosza
			double** weightsSumInBins = new double* [noBins];
			for (int b = 0; b < noBins; b++)
			{
				weightsSumInBins[b] = new  double[attributesCount];
				for (int j = 0; j < attributesCount; j++)
					weightsSumInBins[b][j] = 0;
			}
			double** positivesWeightsSumInBins = new double* [noBins];
			for (int b = 0; b < noBins; b++)
			{
				positivesWeightsSumInBins[b] = new  double[attributesCount];
				for (int j = 0; j < attributesCount; j++)
					positivesWeightsSumInBins[b][j] = 0;
			}

			for (int i = 0; i < indicesCount; i++)
			{
				for (int j = 0; j < attributesCount; j++)
				{
					weightsSumInBins[xInBin[samplesIndices[i]][j]][j] += weightAda[samplesIndices[i]];
					if (D[samplesIndices[i]] == 1)
						positivesWeightsSumInBins[xInBin[samplesIndices[i]][j]][j] += weightAda[samplesIndices[i]];
				}
			}

			// wybor cechy i progu
			double informationValue = INFINITY;
			for (int c = 0; c < attributesCount; c++)
			{
				// pocz�tkowy pr�g dla danej cechy
				double minimTmp = xRanges[c][0];
				double maximTmp = xRanges[c][1];
				double krok = (maximTmp - minimTmp) / noBins;
				double thr = minimTmp + krok;
				double probabilityQuotient;

				// liczba pr�bek pozytwynych po danej stronie progu
				double positivesWeightsLeftSum = positivesWeightsSumInBins[0][c];
				double positivesWeightsRightSum = 0;
				for (int b = 1; b < noBins; b++)
					positivesWeightsRightSum += positivesWeightsSumInBins[b][c];

				// liczba pr�bek po danej stronie progu
				double weightsSumLeft = weightsSumInBins[0][c];
				double weightsSumRight = 0.0;
				for (int b = 1; b < noBins; b++)
					weightsSumRight += weightsSumInBins[b][c];

				double informationTmp = information(positivesWeightsLeftSum, weightsSumLeft, positivesWeightsRightSum, weightsSumRight);

				// zaktualizowanie danych wezla
				if (informationTmp < informationValue)
				{
					informationValue = informationTmp;
					this->feature = c;
					this->threshold = thr;

					savedPositivesProbabilityLeft = positivesWeightsLeftSum / weightsSumLeft;
					savedPositivesProbabilityRight = positivesWeightsRightSum / weightsSumRight;

					probabilityQuotient = positivesWeightsLeftSum / (weightsSumLeft - positivesWeightsLeftSum);
					if (isnan(probabilityQuotient))
						responseLeft = 0.0;
					else if (probabilityQuotient < exp(-4))
						responseLeft = -2.0;
					else if (probabilityQuotient > exp(4))
						responseLeft = 2.0;
					else
						responseLeft = 0.5 * log(probabilityQuotient);

					probabilityQuotient = positivesWeightsRightSum / (weightsSumRight - positivesWeightsRightSum);
					if (isnan(probabilityQuotient))
						responseRight = 0.0;
					else if (probabilityQuotient < exp(-4))
						responseRight = -2.0;
					else if (probabilityQuotient > exp(4))
						responseRight = 2.0;
					else
						responseRight = 0.5 * log(probabilityQuotient);

					savedThresholdBinNumber = 0;
				}

				// sprawdzenie kolejnych progow
				for (int b = 1; b < noBins; b++)
				{
					// aktualizacja progu
					thr += krok;

					// jesli w koszu nie ma probek, nie sprawdza go
					if (weightsSumInBins[b][c] == 0.0)
						continue;

					// aktualizacja licznosci
					positivesWeightsLeftSum += positivesWeightsSumInBins[b][c];
					positivesWeightsRightSum -= positivesWeightsSumInBins[b][c];

					weightsSumLeft += weightsSumInBins[b][c];
					weightsSumRight -= weightsSumInBins[b][c];

					informationTmp = information(positivesWeightsLeftSum, weightsSumLeft, positivesWeightsRightSum, weightsSumRight);

					// zaktualizowanie danych wezla
					if (informationTmp < informationValue)
					{
						informationValue = informationTmp;
						this->feature = c;
						this->threshold = thr;

						savedPositivesProbabilityLeft = positivesWeightsLeftSum / weightsSumLeft;
						savedPositivesProbabilityRight = positivesWeightsRightSum / weightsSumRight;

						probabilityQuotient = positivesWeightsLeftSum / (weightsSumLeft - positivesWeightsLeftSum);
						if (isnan(probabilityQuotient))
							responseLeft = 0.0;
						else if (probabilityQuotient < exp(-4))
							responseLeft = -2.0;
						else if (probabilityQuotient > exp(4))
							responseLeft = 2.0;
						else
							responseLeft = 0.5 * log(probabilityQuotient);

						probabilityQuotient = positivesWeightsRightSum / (weightsSumRight - positivesWeightsRightSum);
						if (isnan(probabilityQuotient))
							responseRight = 0.0;
						else if (probabilityQuotient < exp(-4))
							responseRight = -2.0;
						else if (probabilityQuotient > exp(4))
							responseRight = 2.0;
						else
							responseRight = 0.5 * log(probabilityQuotient);

						savedThresholdBinNumber = b;
					}
				}
			}

			for (int b = 0; b < noBins; b++)
				delete[] weightsSumInBins[b];
			delete[] weightsSumInBins;
			for (int b = 0; b < noBins; b++)
				delete[] positivesWeightsSumInBins[b];
			delete[] positivesWeightsSumInBins;

			// Wybor probek dla dzieci
			int leftSamples = 0;
			int rightSamples = 0;
			for (int j = 0; j < indicesCount; j++)
			{
				if (xInBin[samplesIndices[j]][feature] <= savedThresholdBinNumber)
					leftSamples++;
				else
					rightSamples++;
			}
			int* leftIndices = new int[leftSamples];
			int* rightIndices = new int[rightSamples];
			int left = 0, right = 0;
			for (int j = 0; j < indicesCount; j++)
			{
				if (xInBin[samplesIndices[j]][feature] <= savedThresholdBinNumber)
				{
					leftIndices[left] = samplesIndices[j];
					left++;
				}
				else
				{
					rightIndices[right] = samplesIndices[j];
					right++;
				}
			}

			if (level + 1 < maxLevel)
			{
#pragma omp parallel sections 
				{
#pragma omp section
					{
						if (leftSamples > 0 && savedPositivesProbabilityLeft != 0.0 && savedPositivesProbabilityLeft != 1.0)
						{
							this->Left = new Node("LEFT", level + 1, id + "_0");
							this->Left->train(xInBin, xRanges, D, leftSamples, attributesCount, leftIndices, noBins, maxLevel, weightAda, impurityMetric);
						}
					}
#pragma omp section
					{
						if (rightSamples > 0 && savedPositivesProbabilityRight != 0.0 && savedPositivesProbabilityRight != 1.0)
						{
							this->Right = new Node("RIGHT", level + 1, id + "_1");
							this->Right->train(xInBin, xRanges, D, rightSamples, attributesCount, rightIndices, noBins, maxLevel, weightAda, impurityMetric);
						}
					}
				}
			}

			delete[] leftIndices;
			delete[] rightIndices;
		}

		static void GetUniqueFeatures(unordered_set<int>& features, Node* startingNode)
		{
			features.insert(startingNode->feature);
			if (startingNode->Left != nullptr)
				GetUniqueFeatures(features, startingNode->Left);
			if (startingNode->Right != nullptr)
				GetUniqueFeatures(features, startingNode->Right);
		}
	};

	Node* root = nullptr; /// <summary>Korzen drzewa</summary>
	int maxLevel; /// <summary>Maksymalna glebokosc drzewa</summary>
	string impurityMetric; /// <summary> Miara zaneiczyszczen </summary>

						   /// <summary>Aktualizuje liste cech uzytych w drzewie</summary>
						   /// <param name = 'X'>Wezel, z ktorego powinine pobrac cechy</param>

public:
	using BoostableBinnedClassifier::train;
	using Classifier::classify;
	using Classifier::calculateOutput;
	using Classifier::loadModel;
	using Classifier::saveModel;

	~BinnedTree() override { delete root; }

	/// <summary>Utworzenie TreeBS na podstawie domyslnych parametrow</summary>
	BinnedTree()
	{
		this->maxLevel = 3;
		this->B = 8;
		this->impurityMetric = "Gini";
		this->outlayerPercent = 0.0;
	}

	/// <summary>Utworzenie TreeBS na podstawie strukury z parametrami</summary>
	/// <param name = 'parameters'>Struktura z parametrami dla klasyfiaktora</param>
	BinnedTree(const ClassifierParameters& parameters)
	{
		this->maxLevel = parameters.maxTreeLevel;
		this->B = parameters.treeBins;
		this->impurityMetric = parameters.impurityMetric;
		this->outlayerPercent = parameters.outlayerPercent;
	}

	BinnedTree(BinnedTree* toCopy)
	{
		this->maxLevel = toCopy->maxLevel;
		this->B = toCopy->B;
		this->impurityMetric = toCopy->impurityMetric;
		this->outlayerPercent = toCopy->outlayerPercent;

		this->root = new Node(toCopy->root);

		this->featuresCount = toCopy->featuresCount;
		this->features = new int[featuresCount];
		for (int i = 0; i < featuresCount; i++)
			this->features[i] = toCopy->features[i];
	}

	BinnedTree(int maxLevel, int treeBins, string impurityMetric = "Gini")
	{
		this->maxLevel = maxLevel;
		this->B = treeBins;
		this->impurityMetric = impurityMetric;
		this->outlayerPercent = 0.0;
	}

	BinnedTree(int maxLevel, int treeBins, double outlayers, string impurityMetric = "Gini")
	{
		this->maxLevel = maxLevel;
		this->B = treeBins;
		this->impurityMetric = impurityMetric;
		this->outlayerPercent = outlayers;
	}

	/// <summary>Zaladowanie TreeBS z pliku o podanej sciezce</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	BinnedTree(string path) { loadModel(path); }

	/// <summary>Zaladowanie TreeBS z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	BinnedTree(ifstream& input) { loadModel(input); }

	BinnedTree(ifstream& input, ClassifierParameters& parameter) { loadModel(input, parameter); }

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	static string GetType()
	{
		return "BinnedTree";
	}

	/// <summary>Zwraca typ klasyfikatora</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	/// <summary>Zwraca opis klasyfikatora</summary>
	/// <param name = 'full'>Pe�ny/Skr�cony opis klasyfikatora</param>
	/// <returns>Opis klasyfikatora</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Number of bins: " + to_string(B) + "\r\n";
		text += impurityMetric + "\r\n";
		if (root != NULL)
		{
			text += "Features: ";
			for (int i = 0; i < featuresCount; i++)
				text += to_string(features[i]) + " ";
			text += "\r\n";

			text += "Maximum depth: " + to_string(maxLevel) + "\r\n";
		}
		return text;
	}

	/// <summary>Zaladowanie modelu z podanego strumienia</summary>
	/// <param name = 'input'>Strumien do pliku</param>
	void loadModel(ifstream& input) override
	{
		string fieldName, type;

		skipHeader(input);

		input >> fieldName >> type;
		if (type == getType())
		{
			double fileVer;
			input >> fieldName >> fileVer;

			skipHeader(input);
			input >> fieldName >> B;
			input >> fieldName >> featuresCount;
			if (features != nullptr)
				delete[] features;
			features = new int[featuresCount];
			for (int i = 0; i < featuresCount; i++)
				input >> features[i];

			skipHeader(input);
			input >> fieldName >> outlayerPercent;
			input >> fieldName >> maxLevel;
			input >> fieldName >> impurityMetric;

			if (root != nullptr)
				delete root;
			root = Node::loadNode(input);
		}
		else
			throw ERRORS::CORRUPTED_CLASSIFIER_FILE;
	}

	void loadModel(ifstream& input, ClassifierParameters& parameters) override
	{
		loadModel(input);

		parameters.outlayerPercent = outlayerPercent;
		parameters.treeBins = B;
		parameters.maxTreeLevel = maxLevel;
		strncpy_s(parameters.impurityMetric, ClassifierParameters::STRING_BUFFER, impurityMetric.c_str(), _TRUNCATE);
	}

	/// <summary>Zapisanie modelu do podanego strumienia</summary>
	/// <param name = 'output'>Strumien do pliku</param>
	void saveModel(ofstream& output) const override
	{
		createMainHeader(output, "Classifier_Info:");
		output << "Type: " << getType() << endl;
		output << "Save_Format: 2.0" << endl;
		createSecondaryHeader(output, "Model:");
		output << "Bins: " << B << endl;
		output << "Features_Count: " << featuresCount << endl;
		for (int i = 0; i < featuresCount; i++)
			output << features[i] << " ";
		output << endl;
		createSecondaryHeader(output, "Training_Parameters:");
		output << "Outlayers_Percentage: " << outlayerPercent << endl;
		output << "Max_Depth: " << maxLevel << endl;
		output << "Impurity_Metric: " << impurityMetric << endl;

		root->saveNode(output);
	}

	void saveModelOld(ofstream& output) const override
	{
		output << getType() << endl;
		output << impurityMetric << endl;
		output << B << endl;
		output << maxLevel << endl;
		output << featuresCount << endl;
		for (int i = 0; i < featuresCount; i++)
			output << features[i] << endl;
		root->saveNodeOld(output);
	}

	/// <summary>Uczenie klasyfikatora</summary>
	/// <param name = 'xInBin'>Numer kosza, do ktorego nalezy dana wartosc</param>
	/// <param name = 'xRanges'>Zakres cech</param>
	/// <param name = 'D'>Klasy do nauki</param>
	/// <param name = 'boostingWeights'>Wagi dla boostingu</param>
	/// <param name = 'samples'>Liczba probek</param>
	/// <param name = 'attributes'>Liczba atrybutow</param>
	void train(const int* const* xInBin, const double* const* xRanges, const int* D, const int* indices, const double* boostingWeights, int samplesCount, int attributesCount, int indicesCount) override
	{
		unordered_set<int> fet;

		if (root != nullptr)
			delete root;
		root = new Node();
		root->train(xInBin, xRanges, D, indicesCount, attributesCount, indices, B, maxLevel, boostingWeights, impurityMetric);
		Node::GetUniqueFeatures(fet, root);

		if (features != nullptr)
			delete[] features;
		features = new int[(int)fet.size()];
		int i = 0;
		for (int feature : fet)
		{
			features[i] = feature;
			i++;
		}
		featuresCount = i;
	}

	/// <summary>Wyznacznie wyjsc z klasyfikatora bez ich progowania dla pojedynczej probki</summary>
		/// <param name = 'X'>Cechy pr�bki do klasyfikacji</param>
		/// <returns>Odpowiedz klasyfikatora</returns>
	inline double calculateOutput(const double* X, int attributes) const override
	{
		Node* actualNode = root;
		if (actualNode != nullptr)
		{
			while (true)
			{
				if (X[actualNode->feature] <= (actualNode->threshold))
				{
					if (actualNode->Left != nullptr)
						actualNode = actualNode->Left;
					else
						return actualNode->responseLeft;
				}
				else
				{
					if (actualNode->Right != nullptr)
						actualNode = actualNode->Right;
					else
						return actualNode->responseRight;
				}
			}
		}
		else
			return -1;
	}
};