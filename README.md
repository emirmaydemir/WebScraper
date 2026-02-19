# WebScraper
isinolsun.com web scraper

# Setup Instructions
The application requires the cityconfig.json file to be placed in the bin/Debug directory. This file contains the full list of cities, which the system relies on to operate correctly.

# How the Application Works

# Reading City Data:
The application loads all city information directly from the cityconfig.json file located under bin/Debug.

# Generating Output Files:
As each city is successfully processed, an Excel file is generated in the Output directory. These files contain the completed data for each individual city.

# Error Handling & Logging:
Any errors that occur during city processing result in a log file being created under the Logs directory.
Each log file is named after the city where the error happened, allowing quick identification and troubleshooting.
