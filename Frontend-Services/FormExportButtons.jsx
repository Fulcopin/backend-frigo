import React, { useState } from 'react';
import { exportFormToPDF, exportMultipleFormsToPDF } from './pdfExportService';
import { exportFormToExcel, exportMultipleFormsToExcel, exportConsolidatedDataToExcel } from './excelExportService';

/**
 * Componente de Ejemplo para Exportación de Formularios
 * Demuestra cómo usar los servicios de exportación PDF y Excel
 */
const FormExportButtons = ({ formId, selectedFormIds = [] }) => {
  const [isExporting, setIsExporting] = useState(false);
  const [exportMessage, setExportMessage] = useState('');

  /**
   * Maneja la exportación individual a PDF
   */
  const handleExportPDF = async () => {
    if (!formId) {
      setExportMessage('Error: No hay formulario seleccionado');
      return;
    }

    setIsExporting(true);
    setExportMessage('Generando PDF...');

    try {
      const result = await exportFormToPDF(formId);
      setExportMessage(`✓ PDF generado: ${result.fileName}`);
      
      // Limpiar mensaje después de 3 segundos
      setTimeout(() => setExportMessage(''), 3000);
    } catch (error) {
      setExportMessage(`✗ Error: ${error.message}`);
    } finally {
      setIsExporting(false);
    }
  };

  /**
   * Maneja la exportación individual a Excel
   */
  const handleExportExcel = async () => {
    if (!formId) {
      setExportMessage('Error: No hay formulario seleccionado');
      return;
    }

    setIsExporting(true);
    setExportMessage('Generando Excel...');

    try {
      const result = await exportFormToExcel(formId);
      setExportMessage(`✓ Excel generado: ${result.fileName}`);
      
      setTimeout(() => setExportMessage(''), 3000);
    } catch (error) {
      setExportMessage(`✗ Error: ${error.message}`);
    } finally {
      setIsExporting(false);
    }
  };

  /**
   * Maneja la exportación múltiple a PDF
   */
  const handleExportMultiplePDF = async () => {
    if (!selectedFormIds || selectedFormIds.length === 0) {
      setExportMessage('Error: No hay formularios seleccionados');
      return;
    }

    setIsExporting(true);
    setExportMessage(`Generando PDF con ${selectedFormIds.length} formularios...`);

    try {
      const result = await exportMultipleFormsToPDF(selectedFormIds);
      setExportMessage(`✓ PDF generado con ${result.count} formularios: ${result.fileName}`);
      
      setTimeout(() => setExportMessage(''), 3000);
    } catch (error) {
      setExportMessage(`✗ Error: ${error.message}`);
    } finally {
      setIsExporting(false);
    }
  };

  /**
   * Maneja la exportación múltiple a Excel
   */
  const handleExportMultipleExcel = async () => {
    if (!selectedFormIds || selectedFormIds.length === 0) {
      setExportMessage('Error: No hay formularios seleccionados');
      return;
    }

    setIsExporting(true);
    setExportMessage(`Generando Excel con ${selectedFormIds.length} formularios...`);

    try {
      const result = await exportMultipleFormsToExcel(selectedFormIds);
      setExportMessage(`✓ Excel generado con ${result.count} formularios: ${result.fileName}`);
      
      setTimeout(() => setExportMessage(''), 3000);
    } catch (error) {
      setExportMessage(`✗ Error: ${error.message}`);
    } finally {
      setIsExporting(false);
    }
  };

  /**
   * Maneja la exportación consolidada (análisis de datos)
   */
  const handleExportConsolidated = async () => {
    if (!selectedFormIds || selectedFormIds.length === 0) {
      setExportMessage('Error: No hay formularios seleccionados');
      return;
    }

    setIsExporting(true);
    setExportMessage(`Generando Excel consolidado...`);

    try {
      const result = await exportConsolidatedDataToExcel(selectedFormIds);
      setExportMessage(`✓ Datos consolidados: ${result.fileName}`);
      
      setTimeout(() => setExportMessage(''), 3000);
    } catch (error) {
      setExportMessage(`✗ Error: ${error.message}`);
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="form-export-buttons">
      <style jsx>{`
        .form-export-buttons {
          display: flex;
          flex-direction: column;
          gap: 15px;
          padding: 15px;
          background: #f8f9fa;
          border-radius: 8px;
        }

        .export-section {
          display: flex;
          flex-direction: column;
          gap: 10px;
        }

        .export-section h4 {
          margin: 0;
          color: #333;
          font-size: 14px;
          font-weight: 600;
        }

        .button-group {
          display: flex;
          gap: 10px;
          flex-wrap: wrap;
        }

        .export-btn {
          padding: 10px 20px;
          border: none;
          border-radius: 5px;
          font-size: 14px;
          font-weight: 500;
          cursor: pointer;
          transition: all 0.2s;
          display: flex;
          align-items: center;
          gap: 8px;
        }

        .export-btn:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }

        .pdf-btn {
          background: #dc3545;
          color: white;
        }

        .pdf-btn:hover:not(:disabled) {
          background: #c82333;
          transform: translateY(-1px);
          box-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }

        .excel-btn {
          background: #28a745;
          color: white;
        }

        .excel-btn:hover:not(:disabled) {
          background: #218838;
          transform: translateY(-1px);
          box-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }

        .consolidated-btn {
          background: #007bff;
          color: white;
        }

        .consolidated-btn:hover:not(:disabled) {
          background: #0056b3;
          transform: translateY(-1px);
          box-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }

        .export-message {
          padding: 10px;
          border-radius: 5px;
          font-size: 13px;
          font-weight: 500;
          text-align: center;
          min-height: 40px;
          display: flex;
          align-items: center;
          justify-content: center;
        }

        .export-message:empty {
          display: none;
        }

        .export-message.success {
          background: #d4edda;
          color: #155724;
          border: 1px solid #c3e6cb;
        }

        .export-message.error {
          background: #f8d7da;
          color: #721c24;
          border: 1px solid #f5c6cb;
        }

        .export-message.loading {
          background: #d1ecf1;
          color: #0c5460;
          border: 1px solid #bee5eb;
        }

        .divider {
          height: 1px;
          background: #dee2e6;
          margin: 5px 0;
        }

        .icon {
          font-size: 16px;
        }
      `}</style>

      {/* Mensaje de estado */}
      {exportMessage && (
        <div className={`export-message ${
          exportMessage.startsWith('✓') ? 'success' : 
          exportMessage.startsWith('✗') ? 'error' : 
          'loading'
        }`}>
          {exportMessage}
        </div>
      )}

      {/* Exportación Individual */}
      {formId && (
        <div className="export-section">
          <h4>📄 Exportar Formulario Individual</h4>
          <div className="button-group">
            <button
              className="export-btn pdf-btn"
              onClick={handleExportPDF}
              disabled={isExporting}
            >
              <span className="icon">📕</span>
              Descargar PDF
            </button>
            <button
              className="export-btn excel-btn"
              onClick={handleExportExcel}
              disabled={isExporting}
            >
              <span className="icon">📗</span>
              Descargar Excel
            </button>
          </div>
        </div>
      )}

      {/* Exportación Múltiple */}
      {selectedFormIds && selectedFormIds.length > 0 && (
        <>
          <div className="divider"></div>
          <div className="export-section">
            <h4>📚 Exportar Múltiples Formularios ({selectedFormIds.length})</h4>
            <div className="button-group">
              <button
                className="export-btn pdf-btn"
                onClick={handleExportMultiplePDF}
                disabled={isExporting}
              >
                <span className="icon">📕</span>
                PDF Múltiple
              </button>
              <button
                className="export-btn excel-btn"
                onClick={handleExportMultipleExcel}
                disabled={isExporting}
              >
                <span className="icon">📗</span>
                Excel Múltiple
              </button>
              <button
                className="export-btn consolidated-btn"
                onClick={handleExportConsolidated}
                disabled={isExporting}
              >
                <span className="icon">📊</span>
                Datos Consolidados
              </button>
            </div>
          </div>
        </>
      )}

      {/* Mensaje informativo si no hay selección */}
      {!formId && (!selectedFormIds || selectedFormIds.length === 0) && (
        <div style={{ textAlign: 'center', color: '#6c757d', padding: '20px' }}>
          Selecciona uno o más formularios para exportar
        </div>
      )}
    </div>
  );
};

export default FormExportButtons;

/**
 * EJEMPLO DE USO EN TU APLICACIÓN:
 * 
 * // 1. Exportar un formulario individual (desde vista de detalle)
 * <FormExportButtons formId={4} />
 * 
 * // 2. Exportar múltiples formularios (desde lista con checkboxes)
 * const [selectedForms, setSelectedForms] = useState([1, 2, 3, 4, 5]);
 * <FormExportButtons selectedFormIds={selectedForms} />
 * 
 * // 3. Ambos al mismo tiempo
 * <FormExportButtons 
 *   formId={currentFormId} 
 *   selectedFormIds={selectedForms} 
 * />
 */
