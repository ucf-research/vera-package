using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class MatrixContent : SurveyQuestionContent
    {

        // MatrixContent handles the display and response recording for matrix questions


        #region VARIABLES


        [SerializeField] private MatrixHeaderOption headerOptionPrefab;
        [SerializeField] private RectTransform headerOptionsContainer;
        [SerializeField] private MatrixStatement matrixStatementPrefab;
        [SerializeField] private RectTransform matrixStatementContentContainer;
        [SerializeField] private MatrixStatementOption matrixStatementOptionPrefab;

        private List<MatrixStatement> spawnedStatements = new List<MatrixStatement>();


        #endregion


        #region DISPLAY QUESTION / RESPONSE


        public override void DisplayQuestion(VERASurveyQuestionInfo question)
        {
            base.DisplayQuestion(question);

            // Spawn column headers
            foreach (string columnText in question.matrixColumnTexts)
            {
                MatrixHeaderOption header = Instantiate(headerOptionPrefab, headerOptionsContainer);
                header.Initialize(columnText);
            }

            // Spawn one row per statement, each with options matching the column count
            int columnCount = question.matrixColumnTexts.Length;
            foreach (string rowText in question.matrixRowTexts)
            {
                MatrixStatement statement = Instantiate(matrixStatementPrefab, matrixStatementContentContainer);
                statement.Initialize(rowText, columnCount, matrixStatementOptionPrefab);
                spawnedStatements.Add(statement);
            }
        }


        public override string GetResponse()
        {
            List<string> responses = new List<string>();
            foreach (MatrixStatement statement in spawnedStatements)
            {
                responses.Add(statement.GetSelectedIndex().ToString());
            }
            return string.Join(", ", responses);
        }


        public override bool IsAnswered()
        {
            // Matrix requires every row to have a selection
            foreach (MatrixStatement statement in spawnedStatements)
            {
                if (statement.GetSelectedIndex() < 0) return false;
            }
            return true;
        }


        #endregion


    }
}
