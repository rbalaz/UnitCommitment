using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using UnitCommitment.Evolution_algorithm_blocks;
using UnitCommitment.Representation;
using UnitCommitment.Utilities;

namespace UnitCommitment
{
    public class Evolution
    {
        private int evolutionCycles;
        private int initialPopulationCount;
        private int mode;
        private int scale;

        public Evolution(int evolutionCycles, int initialPopulationCount, int mode, int scale)
        {
            this.evolutionCycles = evolutionCycles;
            this.initialPopulationCount = initialPopulationCount;
            this.mode = mode;
            this.scale = scale;
        }

        public void RealiseEvolution()
        {
            // Initialisation
            Initialisation initialisation = new Initialisation(initialPopulationCount, scale);
            List<Individual> population = initialisation.GenerateInitialPopulation();

            // Validation
            foreach (Individual representation in population)
            {
                if (StaticOperations.ValidateIndividual(representation) == false)
                    throw new NotSupportedException();
            }

            // Evaluation
            Evaluation evaluation = new Evaluation(scale);
            foreach (Individual representation in population)
            {
                evaluation.EvaluateIndividual(representation);
            }

            // Evolution cycles
            for (int i = 0; i < evolutionCycles; i++)
            {
                Console.Write("Epoch #" + i + " ");
                // Selection
                Selection selection = new Selection(initialPopulationCount, population);
                List<Individual> parents = selection.SelectParents(2);

                // Genetic operators
                List<Individual> descendants = new List<Individual>();
                GeneticOperators geneticOperators = new GeneticOperators(scale);
                for (int j = 0; j < parents.Count; j = j + 2)
                {
                    descendants.AddRange(geneticOperators.GenerateDescendants(parents[j], parents[j + 1]));
                }

                // Evaluation
                foreach (Individual representation in descendants)
                {
                    evaluation.EvaluateIndividual(representation);
                }

                // Validation
                foreach (Individual representation in population)
                {
                    if (StaticOperations.ValidateIndividual(representation) == false)
                        throw new NotSupportedException();
                }

                // Replacement
                Replacement replacement = new Replacement(population, descendants, initialPopulationCount);
                population = replacement.NextGeneration();

                List<Individual> orderedPopulation = population.OrderBy(item => item.Fitness).ToList();
                Console.WriteLine("Best individual has fitness: " + orderedPopulation[0].Fitness);
            }

            SaveBestIndividualIntoFile(population[0]);
        }

        public void RealiseGroupEvolution()
        {
            // Initialisation
            GroupInitialisation initialisation = new GroupInitialisation(initialPopulationCount, scale);
            List<Individual> population = null;
            if(mode == 1)
                population = initialisation.GenerateGroupInitialPopulation();
            if (mode == 2)
                population = initialisation.GenerateRandomGroupInitialPopulation();

            // Validation
            foreach (Individual representation in population)
            {
                if (StaticOperations.ValidateIndividualWithGroups(representation) == false)
                    throw new NotSupportedException();
            }

            // Evaluation
            Evaluation evaluation = new Evaluation(scale);
            foreach (Individual representation in population)
            {
                evaluation.EvaluateIndividual(representation);
            }

            // Evolution cycles
            for (int i = 0; i < evolutionCycles; i++)
            {
                Console.Write("Epoch #" + i + " ");
                // Selection
                Selection selection = new Selection(initialPopulationCount, population);
                List<Individual> parents = null;
                if(mode == 1)
                    parents = selection.SelectParents(2);
                if (mode == 2)
                    parents = selection.SelectDiverseParents(2, 5);

                // Genetic operators
                List<Individual> descendants = new List<Individual>();
                GroupGeneticOperators geneticOperators = new GroupGeneticOperators(scale);
                for (int j = 0; j < parents.Count; j = j + 2)
                {
                    descendants.AddRange(geneticOperators.GenerateDescendants(parents[j], parents[j + 1]));
                }

                // Evaluation
                foreach (Individual representation in descendants)
                {
                    evaluation.EvaluateIndividual(representation);
                }

                // Replacement
                Replacement replacement = new Replacement(population, descendants, initialPopulationCount);
                population = replacement.NextGeneration();

                List<Individual> orderedPopulation = population.OrderBy(item => item.Fitness).ToList();
                Console.WriteLine("Best individual has fitness: " + orderedPopulation[0].Fitness);
            }

            SaveBestIndividualIntoFile(population[0]);
        }

        private void SaveBestIndividualIntoFile(Individual representation)
        {
            FileStream memoryStream = new FileStream("riesenie.pdf", FileMode.OpenOrCreate, FileAccess.Write);

            Document document = new Document(PageSize.A4, 10, 10, 10, 10);
            PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);

            document.Open();
            PdfPTable table = new PdfPTable(10*scale + 2)
            {
                SpacingBefore = 3,
                SpacingAfter = 3,
                HorizontalAlignment = Element.ALIGN_LEFT
            };
            Font font6 = FontFactory.GetFont(FontFactory.TIMES, BaseFont.CP1250, BaseFont.EMBEDDED, 6f);
            Font boldFont6 = FontFactory.GetFont(FontFactory.TIMES_BOLD, BaseFont.CP1250, BaseFont.EMBEDDED,6f);
            List<float> widths = new List<float>(); 
            for (int i = 0; i < scale; i++)
            {
                for(int j = 0; j < 10; j++)
                    widths.Add(60f);
            }
            widths.AddRange(new List<float> { 60f, 60f });
            table.SetWidths(widths.ToArray());

            // Row 1
            PdfPCell cell11 = CreateAlignedCell("S.No", boldFont6);
            cell11.Rowspan = 2;
            table.AddCell(cell11);
            PdfPCell cell12 = CreateAlignedCell("Load \n (MW)", boldFont6);
            cell12.Rowspan = 2;
            table.AddCell(cell12);
            PdfPCell cell13 = CreateAlignedCell("Commitment schedule", font6);
            cell13.Colspan = 10 * scale;
            table.AddCell(cell13);

            // Row 2
            for (int i = 1; i < 1 + 10 * scale; i++) 
            {
                PdfPCell numberCell = CreateAlignedCell(i + "", boldFont6);
                table.AddCell(numberCell);
            }

            // Row 3 - 26
            List<int> hourlyLoads = StaticOperations.GenerateHourlyLoads(scale);
            for (int i = 1; i < 25; i++)
            {
                PdfPCell iteratorCell = CreateAlignedCell(i + "", font6);
                table.AddCell(iteratorCell);
                PdfPCell loadCell = CreateAlignedCell(hourlyLoads[i - 1] + "", font6);
                table.AddCell(loadCell);
                for (int j = 0; j < representation.Schedule[i - 1].Count; j++)
                {
                    PdfPCell unitCell = CreateAlignedCell(
                        String.Format("{0:0.#}", representation.Schedule[i-1][j].CurrentOutputPower), font6);
                    table.AddCell(unitCell);
                }
            }
            document.Add(table);

            Paragraph paragraph = new Paragraph("Total operating cost: " + representation.Fitness + "$.")
            {
                SpacingBefore = 3,
                SpacingAfter = 3,
                Font = FontFactory.GetFont(FontFactory.TIMES, 9, BaseColor.BLACK),
                Alignment = 0
            };
            document.Add(paragraph);

            if (representation.groups != null)
            {
                int counter = 0;
                foreach (Group group in representation.groups)
                {
                    counter++;
                    StringBuilder builder = new StringBuilder();
                    builder.Append("Group " + counter + ": ");
                    foreach (int machine in group.grouppedMachines)
                    {
                        builder.Append((machine + 1) + ", ");
                    }
                    string final = builder.ToString().TrimEnd(' ').TrimEnd(',');
                    Paragraph groupParagraph = new Paragraph(final)
                    {
                        SpacingBefore = 3,
                        SpacingAfter = 3,
                        Font = FontFactory.GetFont(FontFactory.TIMES, 9, BaseColor.BLACK),
                        Alignment = 0
                    };
                    document.Add(groupParagraph);
                }
            }

            document.Close();
            memoryStream.Close();
        }

        private PdfPCell CreateAlignedCell(string text, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = 1,
                VerticalAlignment = 1
            };
            return cell;
        }
    }
}
