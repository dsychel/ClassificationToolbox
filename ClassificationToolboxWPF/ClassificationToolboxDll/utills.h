#pragma once
#include<tuple>
#include<fstream>
#include<algorithm>
#include<chrono>
#include<numeric>


#include<sstream>
#include<regex>
#include<iomanip>
#include<map>
#include<complex>
#include<omp.h>
#include<filesystem>

#include"configuration.h"

using namespace std;

enum SaveFileType
{
	text = 1,
	binary8bit = 2,
	binary64bit = 3,

	jpeg = 4,
	bitmap = 5,
	png = 6
};

struct Rectangle
{
public:
	int x, y, w, h;

	Rectangle()
	{
		this->x = 0;
		this->y = 0;
		this->w = 0;
		this->h = 0;
	}

	Rectangle(int x, int y, int w, int h)
	{
		this->x = x;
		this->y = y;
		this->w = w;
		this->h = h;
	}
};

struct Point
{
public:
	int wx;
	int wy;

	Point()
	{
		this->wx = 0;
		this->wy = 0;
	}

	Point(int wx, int wy)
	{
		this->wx = wx;
		this->wy = wy;
	}
};

// ------ IO ------//
#pragma region IO
/// <summary>Wczytanie zbioru z pliku (X, D, Samples, Attributes)</summary>
/// <param name = 'path'>Sciezka do pliku z probkami</param>
/// <returns>X, D, Samples, Attributes</returns>
tuple<double**, int*, int, int> readBinary(const string &path)
{
	ifstream myFile(path, ios_base::binary);

	// wczytanie rozmiaru macierzy
	int n1, n2;
	myFile.read(reinterpret_cast<char*>(&n1), sizeof(n1));
	myFile.read(reinterpret_cast<char*>(&n2), sizeof(n2));

	double** X = new double*[n1];
	int* D = new int[n1];

	// wczytanie kolejnych probek oraz ich klas
	for (int i = 0; i < n1; i++)
	{
		X[i] = new double[n2];

		myFile.read(reinterpret_cast<char*>(X[i]), sizeof(double) * n2);
		myFile.read(reinterpret_cast<char*>(&D[i]), sizeof(int));
	}
	myFile.close();

	return make_tuple(X, D, n1, n2);
}


void  readBinaryPartial(int &n1, int &n2, ifstream &myFile)
{
	myFile.read(reinterpret_cast<char*>(&n1), sizeof(n1));
	myFile.read(reinterpret_cast<char*>(&n2), sizeof(n2));
}

tuple<double**, int*>  readBinaryPartial(ifstream &myFile, int from, int to, int n2)
{
	int offset = to - from;
	double** X = new double*[offset];
	int* D = new int[offset];

	// wczytanie kolejnych probek oraz ich klas
	for (int i = from, j = 0; i < to; i++, j++)
	{
		X[j] = new double[n2];

		myFile.read(reinterpret_cast<char*>(X[j]), sizeof(double) * n2);
		myFile.read(reinterpret_cast<char*>(&D[j]), sizeof(int));
	}

	return make_tuple(X, D);
}

/// <summary>Zapis danych do pliku binarneego</summary>
/// <param name = 'X'>Cechy próbek</param>
/// <param name = 'D'>Klasy próbek</param>
/// <param name = 'path'>Sciezka do pliku</param>
void writeBinary(const double* const* X, const int* D, int n1, int n2, const string path, bool append)
{
	bool exist = filesystem::exists(path);

	if (!append || !exist)
	{
		ofstream myFile(path, ios_base::binary, ios_base::trunc);

		// zapis rozmiaru macierzy
		myFile.write(reinterpret_cast<char*>(&n1), sizeof(n1));
		myFile.write(reinterpret_cast<char*>(&n2), sizeof(n2));

		// zapis kolejnych probek wraz z ich klasa
		for (int i = 0; i < n1; i++)
		{
			myFile.write(reinterpret_cast<const char*>(X[i]), sizeof(double)*n2);
			myFile.write(reinterpret_cast<const char*>(&D[i]), sizeof(int));
		}
		myFile.close();
	}
	else
	{
		int n1_old;
		int n2_old;

		fstream myFile(path, ios_base::out | ios_base::in | ios_base::ate | ios_base::binary);
		myFile.seekg(0);
		myFile.read(reinterpret_cast<char*>(&n1_old), sizeof(n1_old));
		myFile.read(reinterpret_cast<char*>(&n2_old), sizeof(n2_old));
		if (n2 != n2_old)
		{
			myFile.close();
			throw ERRORS::INCONSISTENT_FEATURES;
		}

		int samples = n1 + n1_old;
		myFile.seekp(0, ios::end);

		for (int i = 0; i < n1; i++)
		{
			myFile.write(reinterpret_cast<const char*>(X[i]), sizeof(double)*n2);
			myFile.write(reinterpret_cast<const char*>(&D[i]), sizeof(int));
		}
		myFile.seekp(0);
		myFile.write(reinterpret_cast<char*>(&samples), sizeof(samples));
		myFile.close();
	}
}

/// <summary>Zwalnia zbiór danych z pamieci</summary>
/// <param name = 'X'>Probki</param>
/// <param name = 'D'>Klasy</param>
/// <param name = 'n1'>Liczba probek</param>
void clearData(double** &X, int* &D, int n1)
{
	if (X != nullptr)
	{
		for (int i = 0; i < n1; i++)
			delete[] X[i];
		delete[] X;
	}
	if (D != nullptr)
		delete[] D;

	X = nullptr;
	D = nullptr;
}

/// <summary>Zwalnia zbiór danych z pamieci</summary>
/// <param name = 'X'>Probki</param>
/// <param name = 'D'>Klasy</param>
/// <param name = 'n1'>Liczba probek</param>
void clearData(const double* const* &X, int* &D, int n1)
{
	if (X != nullptr)
	{
		for (int i = 0; i < n1; i++)
			delete[] X[i];
		delete[] X;
	}
	if (D != nullptr)
		delete[] D;

	X = nullptr;
	D = nullptr;
}

/// <summary>Wczytanie obrazu z pliku tekstowego</summary>
/// <param name = 'img'>Wczytany obraz</param>
/// <param name = 'path'>Sciezka do pliku</param>
/// <param name = 'fileType'>Rodzaj pliku (tesktowy, binarny [double], binarny [byte])</param>
tuple<int, int, const double * const*> loadImage(string path, SaveFileType fileType)
{
	double** img = nullptr;

	if (fileType == SaveFileType::text)
	{
		ifstream file(path, ios::in);
		string line;
		double tmp;
		int number_of_lines = 0;
		int number_of_elements = 0;
		while (getline(file, line))
		{
			if (number_of_lines == 0)
			{
				istringstream ss(line);
				while (ss >> tmp)
					++number_of_elements;
			}
			++number_of_lines;
		}

		img = new double*[number_of_lines];
		for (int i = 0; i < number_of_lines; i++)
			img[i] = new double[number_of_elements];

		file.clear();
		file.seekg(0, ios::beg);

		number_of_lines = 0;
		while (getline(file, line))
		{
			istringstream ss(line);

			int i = 0;
			while (ss >> tmp)
			{
				img[number_of_lines][i] = tmp;
				++i;
			}
			++number_of_lines;
		}
		file.close();

		return make_tuple(number_of_lines, number_of_elements, img);
	}
	else if (fileType == SaveFileType::binary8bit)
	{
		ifstream myFile(path, std::ios_base::binary);

		// wczytanie rozmiaru macierzy
		int width, height;
		myFile.read(reinterpret_cast<char*>(&width), sizeof(width));
		myFile.read(reinterpret_cast<char*>(&height), sizeof(height));

		img = new double*[height];
		for (int i = 0; i < height; i++)
			img[i] = new double[width];

		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				unsigned char value;
				myFile.read(reinterpret_cast<char*>(&value), sizeof(value));
				img[i][j] = value / 255.0;
			}
		}
		myFile.close();

		return make_tuple(height, width, img);
	}
	else if (fileType == SaveFileType::binary64bit)
	{
		ifstream myFile(path, std::ios_base::binary);

		// wczytanie rozmiaru macierzy
		int width, height;
		myFile.read(reinterpret_cast<char*>(&width), sizeof(width));
		myFile.read(reinterpret_cast<char*>(&height), sizeof(height));

		img = new double*[height];
		for (int i = 0; i < height; i++)
			img[i] = new double[width];

		for (int i = 0; i < height; i++)
		{
			myFile.read(reinterpret_cast<char*>(img[i]), sizeof(double) * width);
		}
		myFile.close();

		return make_tuple(height, width, img);
	}
	return make_tuple(0, 0, img);
}
#pragma endregion Read/write functions
// -----! IO !-----//

// ------ IMAGE ------//
#pragma region  IMAGE
const double* const* ConvertToGray(const unsigned char* const image, int bytesPerPixel, int width, int height, int stride)
{
	double** grayImage = new double*[height];
	for (int i = 0; i < height; i++)
		grayImage[i] = new double[width];

	// czarno bia³y
	if (bytesPerPixel == 1)
	{
		for (int h = 0; h < height; h++)
		{
			for (int w = 0; w < width; w++)
			{
				grayImage[h][w] = image[h * stride + w] / 255.0;
			}
		}
	}
	// RGB
	else if (bytesPerPixel == 3)
	{
		for (int h = 0; h < height; h++)
		{
			for (int w = 0; w < width; w++)
			{
				int id = h * stride + w * bytesPerPixel;
				grayImage[h][w] = round((0.1140 * image[id + 0] + 0.5870 * image[id + 1] + 0.2990 * image[id + 2])) / 255.0;
			}
		}
	}
	// ARGB
	else if (bytesPerPixel == 4)
	{
		for (int h = 0; h < height; h++)
		{
			for (int w = 0; w < width; w++)
			{
				int id = h * stride + w * bytesPerPixel;
				double alpha = image[id + 3] / 255.0;
				grayImage[h][w] = round((0.1140 * image[id + 0] + 0.5870 * image[id + 1] + 0.2990 * image[id + 2]) * alpha) / 255.0;
			}
		}
	}
	else
		throw UNSUPPORTED_IMAGE_FORMAT;

	return grayImage;
}
#pragma endregion Image functions
// -----! IMAGE !-----//

// ------ SORTING ------ //
#pragma region SORTING
template <typename T>
void sort_indexes(const T* const* X, int* idx, int samples, int feature)
{
	sort(idx, idx + samples,
		[&X, &feature](int i1, int i2) {return X[i1][feature] < X[i2][feature]; });
}

template <typename T>
void sort_indexes_dsc(const T* X, int* idx, int samples)
{
	sort(idx, idx + samples,
		[&X](int i1, int i2) {return X[i1] > X[i2]; });
}

template <typename T>
void sort_indexes(const T* X, int* idx, int samples)
{
	sort(idx, idx + samples,
		[&X](int i1, int i2) {return X[i1] < X[i2]; });
}

#pragma endregion Sorting Functions
// -----! SORTING !----- //

// ------ COMBINATORIC ------ //
#pragma region COMBINATORIC
tuple<const double* const*, const int*> randperm(const double* const* X, const int* D, int samplesCount, int positives)
{
	srand((unsigned int)time(NULL));
	const double** Xout = new const double *[samplesCount];
	int* Dout = new int[samplesCount];

	vector<int> indices(positives);
	iota(indices.begin(), indices.end(), 0);
	for (int i = (int)indices.size() - 1, j = 0; i >= 0; i--, j++)
	{
		int tmp = rand() % (indices.size());
		int index = indices[tmp];

		Xout[j] = X[index];
		Dout[j] = D[index];

		indices.erase(indices.begin() + tmp);
	}

	vector<int> indicesNegative(samplesCount - positives);
	iota(indicesNegative.begin(), indicesNegative.end(), positives);
	for (int i = (int)indicesNegative.size() - 1, j = positives; i >= 0; i--, j++)
	{
		int tmp = rand() % (indicesNegative.size());
		int index = indicesNegative[tmp];

		Xout[j] = X[index];
		Dout[j] = D[index];

		indicesNegative.erase(indicesNegative.begin() + tmp);
	}

	return make_tuple(Xout, Dout);
}

tuple<const double* const*, const int*, const double* const*, const int*, int, int> split(const double* const* X, const int* D, int samplesCount, int attribiutesCount, double alpha)
{
	int positives = 0;
	for (int i = 0; i < samplesCount; i++)
		if (D[i] == 1)
			positives++;
		else
			break;

	int positivesSplit = (int)floor(positives * alpha);
	int negativesSplit = (int)floor((samplesCount - positives) * alpha) + positives;

	int size1 = positivesSplit + negativesSplit - positives;
	int size2 = samplesCount - size1;

	const double** X1 = new const double*[size1];
	const double** X2 = new const double*[size2];
	int* D1 = new int[size1];
	int* D2 = new int[size2];

	auto[xPermuted, dPermuted] = randperm(X, D, samplesCount, positives);

	// rozdielenie próbek o klasie 1 na dwa zbiory
	int j = 0;
	int k = 0;
	for (int i = 0; i < positivesSplit; i++)
	{
		X1[j] = xPermuted[i];
		D1[j] = dPermuted[i];
		j++;
	}
	for (int i = positivesSplit; i < positives; i++)
	{
		X2[k] = xPermuted[i];
		D2[k] = dPermuted[i];
		k++;
	}

	// rozdielenie próbek o klasie -1 na dwa zbiory
	for (int i = positives; i < negativesSplit; i++)
	{
		X1[j] = xPermuted[i];
		D1[j] = dPermuted[i];
		j++;
	}
	for (int i = negativesSplit; i < samplesCount; i++)
	{
		X2[k] = xPermuted[i];
		D2[k] = dPermuted[i];
		k++;
	}

	delete[] xPermuted;
	delete[] dPermuted;

	return make_tuple(X1, D1, X2, D2, size1, size2);
}

/// <summary>Generowanie parametreow dwumianu</summary>
/// <param name = 'p_max'>maksymalne p</param>
/// <param name = 'q_max'>maksymalne q</param>
/// <param name = 'table'>macierz z parametrami dwumianow</param>
void binomial(int p_max, int q_max, vector<vector<unsigned long long int>> &table)
{
	table.clear();

	int n = p_max + 2 * q_max + 1;
	table.resize(n);
	for (int i = 0; i < n; i++)
		table[i].resize(i + 1);
	table[0][0] = 1;

	for (int w = 1; w < n; w++)
	{
		table[w][0] = 1;
		for (int k = 1; k <= w; k++)
		{
			if (w == k)
				table[w][k] = 1;
			else
				table[w][k] = table[w - 1][k] + table[w - 1][k - 1];
		}
	}
}
#pragma endregion Combinatoric functions
// -----! COMBINATORIC !----- //

// ------ MATH ------//
#pragma region MATH
complex<double> powCom(complex<double> a, int expo)
{
	complex<double> res = complex<double>(1, 0);
	for (int i = 0; i < expo; i++)
		res *= a;
	return res;
}

complex<double> powComQuick(complex<double> a, int expo)
{
	complex<double> res = complex<double>(1, 0);
	while (expo > 0)
	{
		if (expo % 2 == 0)
		{
			expo /= 2;
			a = a * a;
		}
		else
		{
			expo -= 1;
			res *= a;

			expo /= 2;
			a = a * a;
		}
	}
	return res;
}

double powInt(double a, int expo)
{
	double res = 1;
	for (int i = 0; i < expo; i++)
		res *= a;
	return res;
}

double powIntQuick(double a, int expo)
{
	double res = 1;
	while (expo > 0)
	{
		if (expo % 2 == 0)
		{
			expo /= 2;
			a = a * a;
		}
		else
		{
			expo -= 1;
			res *= a;

			expo /= 2;
			a = a * a;
		}
	}
	return res;
}

///// <summary>Wyzanczenie iloczynu skalarnego</summary>
///// <param name = 'x1'>Pierwszy wektor</param>
///// <param name = 'x2'>Drugi wektor</param>
///// <returns>Iloczyn skalarny</returns>
//template <typename T1, typename T2>
//double scalarProd(const vector<T1> &x1, const vector<T2> &x2)
//{
//	double s = 0;
//	for (int i = 0; i < (int)x1.size(); i++)
//		s += x1[i] * x2[i];
//	return s;
//}
//
///// <summary>Wyzanczenie najwiekszego wspolnego dzielnika (NWD)</summary>
///// <param name = 'a'>Pierwsza liczba</param>
///// <param name = 'b'>Druga liczba</param>
///// <returns>Najwiekszy wspolny dzielnik</returns>
//int gcd(int a, int b)
//{
//	while (b != 0)
//	{
//		int r = a % b;
//		a = b;
//		b = r;
//	}
//	return a;
//}
//
/// <summary>Iloczyn elementow wektora</summary>
/// <param name = 'a'>Wektor do wymnozenia</param>
/// <returns>Najwiekszy wspolny dzielnik</returns>
int prod(vector<int> &a)
{
	int w = a[0];
	for (int i = 1; i < (int)a.size(); i++)
		w *= a[i];
	return w;
}

double powReal(double a, int expo)
{
	double res = 1;
	for (int i = 0; i < expo; i++)
		res *= a;
	return res;
}
#pragma endregion Mathematic functions
// -----! MATH !-----//


// ------ ERROR EVALUATION METRICS ------//
#pragma region  ERROR EVALUATION METRICS

/// <summary>Generuje macierz konfuzji oraz oblicza miary jakosci</summary>
/// <param name = 'D'>Orginalne klasy</param>
/// <param name = 'out'>Klasy wyznaczone przez klasyfikator</param>
/// <returns>Macierz konfuzji</returns>
map<string, pair<string, double>> confusionMatrix(const int* D, const int* out, const int samplesCount)
{
	int TP = 0, TN = 0, FP = 0, FN = 0;
	for (int i = 0; i < samplesCount; i++)
	{
		if (D[i] == out[i] && D[i] == 1)
			TP++;
		else if (D[i] == out[i] && D[i] == -1)
			TN++;
		else if (D[i] != out[i] && D[i] == 1 && out[i] == -1)
			FN++;
		else
			FP++;
	}

	map<string, pair<string, double>> measures;
	measures["TP"] = pair<string, double>("True positive (TP)", TP);
	measures["FP"] = pair<string, double>("False positive (FP)", FP);
	measures["TN"] = pair<string, double>("True negative (TN)", TN);
	measures["FN"] = pair<string, double>("False negative (FN)", FN);
	measures["ACC"] = pair<string, double>("Accuracy (ACC)", 1.0 * (TP + TN) / (TP + TN + FP + FN));
	measures["ERR"] = pair<string, double>("Error (ERR)", 1.0 * (FP + FN) / (TP + TN + FP + FN));
	measures["TPR"] = pair<string, double>("Sensitivity (TPR)", 1.0 * (TP) / (TP + FN));
	measures["SPC"] = pair<string, double>("Specificity (SPC)", 1.0 * (TN) / (TN + FP));
	measures["F1"] = pair<string, double>("F1 - score (F1)", 2.0 * (TP) / (2 * TP + FP + FN));
	measures["PPV"] = pair<string, double>("Precision (PPV)", 1.0 * (TP) / (TP + FP));
	measures["NPV"] = pair<string, double>("Negative predictive value (NPV)", 1.0 * (TN) / (TN + FN));
	measures["FPR"] = pair<string, double>("Fall - out (FPR / FAR)", 1.0 * (FP) / (FP + TN));
	measures["FNR"] = pair<string, double>("False negative rate (FNR)", 1.0 * (FN) / (TP + FN));
	measures["FDR"] = pair<string, double>("False discovery rate (FDR)", 1.0 * (FP) / (TP + FP));
	measures["MCC"] = pair<string, double>("Matthews correlation coefficient (MCC)", 1.0 * ((TP * TN) - (FP * FN)) / sqrt(1.0 * (TP + FP) * (TP + FN) * (TN + FP) * (TN + FN)));
	measures["INF"] = pair<string, double>("Informedness (INF)", 1.0 * (measures["TPR"].second + measures["SPC"].second - 1.0));
	measures["MARK"] = pair<string, double>("Markedness (MARK)", 1.0 * (measures["PPV"].second + measures["NPV"].second - 1.0));
	return measures;
}

/// <summary>Oblicza blad klasyfikacji</summary>
/// <param name = 'y'>Orginalne klasy</param>
/// <param name = 'out'>Klasy wyznaczone przez klasyfikator</param>
/// <returns>Blad klasyfikacji</returns>
double calculateError(const int* D, const int* out, const int samplesCount)
{
	double err = 0;
	for (int i = 0; i < samplesCount; i++)
		if (D[i] != out[i])
			err += 1;
	err /= samplesCount;
	return err;
}
#pragma endregion Error evaluation functions
// -----! ERROR EVALUATION METRICS !-----//

// ------ OTHER ------//
#pragma region  OTHER
int isInBin(const double X, const int B)
{
	int isIn = 0;
	if (X <= 0)
		isIn = 0;
	else
		isIn = (int)ceil(B * X) - 1;
	if (isIn >= B)
		isIn = B - 1;
	return isIn;
}

void calculateRanges(const double* const* X, double** minmax, const int* indices, int samples, int features, double outlayerPercent)
{
	if (outlayerPercent > 0.0)
	{
		int outP = (int)(samples * outlayerPercent);

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < features; i++)
		{
			int* sorted = new int[samples];
			memcpy(sorted, indices, sizeof(int) * samples);
			sort_indexes(X, sorted, samples, i);

			minmax[i][0] = X[sorted[outP]][i];
			minmax[i][1] = X[sorted[samples - outP - 1]][i];

			delete[] sorted;
		}
	}
	else
	{
		for (int i = 0; i < features; i++)
		{
			minmax[i][0] = INFINITY;
			minmax[i][1] = -INFINITY;
			for (int j = 0; j < samples; j++)
			{
				if (X[indices[j]][i] < minmax[i][0])
					minmax[i][0] = X[indices[j]][i];
				if (X[indices[j]][i] > minmax[i][1])
					minmax[i][1] = X[indices[j]][i];
			}
		}
	}
}

void assignBins(const double* const* X, const double* const* minmax, int** xInBin, const int* indices, int samples, int features, int bins)
{
#pragma omp parallel for num_threads(OMP_NUM_THR)
	for (int i = 0; i < samples; i++)
	{
		for (int j = 0; j < features; j++)
		{
			double xNorm = (X[indices[i]][j] - minmax[j][0]) / (minmax[j][1] - minmax[j][0]);
			xInBin[indices[i]][j] = isInBin(xNorm, bins);
		}
	}
}
#pragma endregion Other functions
// -----! OTHER !-----//
