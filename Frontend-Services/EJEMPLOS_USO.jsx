/**
 * EJEMPLOS PRÁCTICOS DE USO
 * Casos reales de cómo integrar la exportación en tu aplicación
 */

// ═══════════════════════════════════════════════════════════
// CASO 1: Botón de exportación en vista de detalle de formulario
// ═══════════════════════════════════════════════════════════

import React from 'react';
import { exportFormToPDF } from './pdfExportService';
import { exportFormToExcel } from './excelExportService';

function FormDetailPage({ formId }) {
  return (
    <div className="form-detail">
      <h1>Formulario #{formId}</h1>
      
      <div className="form-content">
        {/* Tu contenido del formulario aquí */}
      </div>
      
      <div className="export-actions">
        <button onClick={() => exportFormToPDF(formId)}>
          📕 Descargar PDF
        </button>
        <button onClick={() => exportFormToExcel(formId)}>
          📗 Descargar Excel
        </button>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 2: Lista de formularios con checkboxes para selección múltiple
// ═══════════════════════════════════════════════════════════

function FormsListPage() {
  const [forms, setForms] = React.useState([]);
  const [selectedIds, setSelectedIds] = React.useState([]);
  
  React.useEffect(() => {
    // Cargar formularios
    fetch('/api/FilledForms')
      .then(res => res.json())
      .then(data => setForms(data));
  }, []);
  
  const handleSelectAll = () => {
    if (selectedIds.length === forms.length) {
      setSelectedIds([]);
    } else {
      setSelectedIds(forms.map(f => f.formID));
    }
  };
  
  const handleToggleForm = (formId) => {
    if (selectedIds.includes(formId)) {
      setSelectedIds(selectedIds.filter(id => id !== formId));
    } else {
      setSelectedIds([...selectedIds, formId]);
    }
  };
  
  const handleExportSelected = async (format) => {
    if (selectedIds.length === 0) {
      alert('Selecciona al menos un formulario');
      return;
    }
    
    try {
      if (format === 'pdf') {
        await exportMultipleFormsToPDF(selectedIds);
      } else {
        await exportMultipleFormsToExcel(selectedIds);
      }
      alert('Exportación completada');
    } catch (error) {
      alert('Error en la exportación: ' + error.message);
    }
  };
  
  return (
    <div className="forms-list">
      <div className="toolbar">
        <button onClick={handleSelectAll}>
          {selectedIds.length === forms.length ? 'Deseleccionar todos' : 'Seleccionar todos'}
        </button>
        
        {selectedIds.length > 0 && (
          <div className="bulk-actions">
            <span>{selectedIds.length} seleccionados</span>
            <button onClick={() => handleExportSelected('pdf')}>
              📕 Exportar a PDF
            </button>
            <button onClick={() => handleExportSelected('excel')}>
              📗 Exportar a Excel
            </button>
          </div>
        )}
      </div>
      
      <table>
        <thead>
          <tr>
            <th>
              <input 
                type="checkbox" 
                checked={selectedIds.length === forms.length}
                onChange={handleSelectAll}
              />
            </th>
            <th>ID</th>
            <th>Fecha</th>
            <th>Template</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          {forms.map(form => (
            <tr key={form.formID}>
              <td>
                <input 
                  type="checkbox"
                  checked={selectedIds.includes(form.formID)}
                  onChange={() => handleToggleForm(form.formID)}
                />
              </td>
              <td>{form.formID}</td>
              <td>{new Date(form.createdAt).toLocaleDateString()}</td>
              <td>{form.templateID}</td>
              <td>
                <button onClick={() => exportFormToPDF(form.formID)}>PDF</button>
                <button onClick={() => exportFormToExcel(form.formID)}>Excel</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 3: Exportación por rango de fechas
// ═══════════════════════════════════════════════════════════

import { exportFormsByDateRangeToPDF, exportFormsByDateRangeToExcel } from './pdfExportService';

function ReportsPage() {
  const [startDate, setStartDate] = React.useState('2025-01-01');
  const [endDate, setEndDate] = React.useState('2025-01-31');
  
  const handleExportByDate = async (format) => {
    try {
      if (format === 'pdf') {
        await exportFormsByDateRangeToPDF(startDate, endDate);
      } else {
        await exportFormsByDateRangeToExcel(startDate, endDate);
      }
      alert('Exportación completada');
    } catch (error) {
      alert('Error: ' + error.message);
    }
  };
  
  return (
    <div className="reports">
      <h2>Reportes por Fecha</h2>
      
      <div className="date-filters">
        <label>
          Desde:
          <input 
            type="date" 
            value={startDate} 
            onChange={e => setStartDate(e.target.value)}
          />
        </label>
        
        <label>
          Hasta:
          <input 
            type="date" 
            value={endDate} 
            onChange={e => setEndDate(e.target.value)}
          />
        </label>
      </div>
      
      <div className="export-buttons">
        <button onClick={() => handleExportByDate('pdf')}>
          📕 Exportar a PDF
        </button>
        <button onClick={() => handleExportByDate('excel')}>
          📗 Exportar a Excel
        </button>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 4: Exportación desde vista de template (todos los formularios)
// ═══════════════════════════════════════════════════════════

import { exportTemplateFormsToPDF, exportTemplateFormsToExcel } from './pdfExportService';

function TemplateDetailPage({ templateId }) {
  const handleExportAllForms = async (format) => {
    try {
      if (format === 'pdf') {
        await exportTemplateFormsToPDF(templateId);
      } else {
        await exportTemplateFormsToExcel(templateId);
      }
      alert('Exportación completada');
    } catch (error) {
      alert('Error: ' + error.message);
    }
  };
  
  return (
    <div className="template-detail">
      <h1>Template #{templateId}</h1>
      
      <div className="template-actions">
        <button onClick={() => handleExportAllForms('pdf')}>
          📕 Exportar todos los formularios a PDF
        </button>
        <button onClick={() => handleExportAllForms('excel')}>
          📗 Exportar todos los formularios a Excel
        </button>
      </div>
      
      {/* Lista de formularios del template */}
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 5: Exportación consolidada para análisis de datos
// ═══════════════════════════════════════════════════════════

import { exportConsolidatedDataToExcel } from './excelExportService';

function DataAnalysisPage() {
  const [selectedForms, setSelectedForms] = React.useState([]);
  
  const handleExportForAnalysis = async () => {
    try {
      // Exporta todas las tablas consolidadas en un solo Excel
      // Útil para análisis en Excel, PowerBI, etc.
      await exportConsolidatedDataToExcel(selectedForms);
      alert('Datos consolidados exportados');
    } catch (error) {
      alert('Error: ' + error.message);
    }
  };
  
  return (
    <div className="analysis">
      <h2>Análisis de Datos</h2>
      <p>
        Exporta datos consolidados para análisis en Excel o PowerBI.
        Las tablas del mismo tipo se agruparán en una sola hoja.
      </p>
      
      {/* Selección de formularios */}
      
      <button onClick={handleExportForAnalysis}>
        📊 Exportar Datos Consolidados
      </button>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 6: Exportación con loading state y notificaciones
// ═══════════════════════════════════════════════════════════

function FormWithLoadingState({ formId }) {
  const [isExporting, setIsExporting] = React.useState(false);
  const [exportStatus, setExportStatus] = React.useState('');
  
  const handleExport = async (format) => {
    setIsExporting(true);
    setExportStatus('Generando archivo...');
    
    try {
      if (format === 'pdf') {
        const result = await exportFormToPDF(formId);
        setExportStatus(`✓ ${result.fileName} descargado`);
      } else {
        const result = await exportFormToExcel(formId);
        setExportStatus(`✓ ${result.fileName} descargado`);
      }
      
      // Limpiar mensaje después de 3 segundos
      setTimeout(() => setExportStatus(''), 3000);
      
    } catch (error) {
      setExportStatus(`✗ Error: ${error.message}`);
      setTimeout(() => setExportStatus(''), 5000);
    } finally {
      setIsExporting(false);
    }
  };
  
  return (
    <div>
      <div className="export-buttons">
        <button 
          onClick={() => handleExport('pdf')} 
          disabled={isExporting}
        >
          {isExporting ? 'Generando...' : '📕 PDF'}
        </button>
        
        <button 
          onClick={() => handleExport('excel')} 
          disabled={isExporting}
        >
          {isExporting ? 'Generando...' : '📗 Excel'}
        </button>
      </div>
      
      {exportStatus && (
        <div className={`status-message ${
          exportStatus.startsWith('✓') ? 'success' : 'error'
        }`}>
          {exportStatus}
        </div>
      )}
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 7: Exportación automática después de guardar formulario
// ═══════════════════════════════════════════════════════════

function FormEditor({ templateId }) {
  const [formData, setFormData] = React.useState({});
  
  const handleSaveAndExport = async () => {
    try {
      // 1. Guardar formulario
      const response = await fetch('/api/FilledForms', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          templateID: templateId,
          headerData: JSON.stringify(formData.header),
          bodyData: JSON.stringify(formData.body),
          firmasData: JSON.stringify(formData.firmas),
          observaciones: formData.observaciones
        })
      });
      
      const savedForm = await response.json();
      
      // 2. Exportar automáticamente
      await exportFormToPDF(savedForm.formID);
      
      alert('Formulario guardado y PDF generado');
      
    } catch (error) {
      alert('Error: ' + error.message);
    }
  };
  
  return (
    <div>
      {/* Editor del formulario */}
      
      <button onClick={handleSaveAndExport}>
        💾 Guardar y Descargar PDF
      </button>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 8: Menú contextual (click derecho)
// ═══════════════════════════════════════════════════════════

function FormRowWithContextMenu({ form }) {
  const [showMenu, setShowMenu] = React.useState(false);
  const [menuPosition, setMenuPosition] = React.useState({ x: 0, y: 0 });
  
  const handleContextMenu = (e) => {
    e.preventDefault();
    setMenuPosition({ x: e.clientX, y: e.clientY });
    setShowMenu(true);
  };
  
  const handleExport = async (format) => {
    setShowMenu(false);
    
    if (format === 'pdf') {
      await exportFormToPDF(form.formID);
    } else {
      await exportFormToExcel(form.formID);
    }
  };
  
  return (
    <>
      <tr onContextMenu={handleContextMenu}>
        <td>{form.formID}</td>
        <td>{form.createdAt}</td>
        <td>{form.templateID}</td>
      </tr>
      
      {showMenu && (
        <div 
          className="context-menu"
          style={{ 
            position: 'fixed', 
            top: menuPosition.y, 
            left: menuPosition.x 
          }}
          onMouseLeave={() => setShowMenu(false)}
        >
          <div onClick={() => handleExport('pdf')}>📕 Exportar a PDF</div>
          <div onClick={() => handleExport('excel')}>📗 Exportar a Excel</div>
          <div onClick={() => setShowMenu(false)}>❌ Cancelar</div>
        </div>
      )}
    </>
  );
}

// ═══════════════════════════════════════════════════════════
// CASO 9: Exportación programada/batch (backend job)
// ═══════════════════════════════════════════════════════════

// Esta función se ejecutaría en un job programado del backend
async function scheduledMonthlyReport() {
  const now = new Date();
  const firstDay = new Date(now.getFullYear(), now.getMonth() - 1, 1);
  const lastDay = new Date(now.getFullYear(), now.getMonth(), 0);
  
  const startDate = firstDay.toISOString().split('T')[0];
  const endDate = lastDay.toISOString().split('T')[0];
  
  try {
    // Exportar todos los formularios del mes anterior
    await exportFormsByDateRangeToExcel(startDate, endDate);
    console.log('Reporte mensual generado');
  } catch (error) {
    console.error('Error generando reporte:', error);
  }
}

// ═══════════════════════════════════════════════════════════
// CASO 10: Exportación con preview antes de descargar
// ═══════════════════════════════════════════════════════════

function FormWithPreview({ formId }) {
  const [previewData, setPreviewData] = React.useState(null);
  const [showPreview, setShowPreview] = React.useState(false);
  
  const handlePreview = async () => {
    try {
      const response = await fetch(`/api/FilledForms/${formId}/with-template`);
      const data = await response.json();
      setPreviewData(data);
      setShowPreview(true);
    } catch (error) {
      alert('Error cargando preview: ' + error.message);
    }
  };
  
  const handleConfirmExport = async (format) => {
    if (format === 'pdf') {
      await exportFormToPDF(formId);
    } else {
      await exportFormToExcel(formId);
    }
    setShowPreview(false);
  };
  
  return (
    <div>
      <button onClick={handlePreview}>👁️ Vista Previa</button>
      
      {showPreview && previewData && (
        <div className="preview-modal">
          <h3>Vista Previa del Formulario</h3>
          
          {/* Renderizar datos de previewData */}
          <pre>{JSON.stringify(previewData, null, 2)}</pre>
          
          <div className="preview-actions">
            <button onClick={() => handleConfirmExport('pdf')}>
              📕 Confirmar y Descargar PDF
            </button>
            <button onClick={() => handleConfirmExport('excel')}>
              📗 Confirmar y Descargar Excel
            </button>
            <button onClick={() => setShowPreview(false)}>
              ❌ Cancelar
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export {
  FormDetailPage,
  FormsListPage,
  ReportsPage,
  TemplateDetailPage,
  DataAnalysisPage,
  FormWithLoadingState,
  FormEditor,
  FormRowWithContextMenu,
  scheduledMonthlyReport,
  FormWithPreview
};
