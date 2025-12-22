import jsPDF from 'jspdf';
import 'jspdf-autotable';

/**
 * Servicio de Exportación a PDF Dinámico
 * Renderiza formularios basándose en la estructura del template
 */

/**
 * Exporta un formulario individual a PDF
 * @param {number} formId - ID del formulario a exportar
 */
export const exportFormToPDF = async (formId) => {
  try {
    // Obtener datos completos del formulario con template
    const response = await fetch(`/api/FilledForms/${formId}/with-template`);
    
    if (!response.ok) {
      throw new Error(`Error al obtener formulario: ${response.status}`);
    }
    
    const formData = await response.json();
    
    // Generar PDF
    const pdf = await generateFormPDF(formData);
    
    // Descargar
    const fileName = `${formData.template.codigo || 'Formulario'}_${formData.formID}_${formatDate(formData.createdAt)}.pdf`;
    pdf.save(fileName);
    
    return { success: true, fileName };
    
  } catch (error) {
    console.error('Error exportando PDF:', error);
    throw error;
  }
};

/**
 * Exporta múltiples formularios a un solo PDF
 * @param {number[]} formIds - Array de IDs de formularios
 */
export const exportMultipleFormsToPDF = async (formIds) => {
  try {
    const response = await fetch('/api/FilledForms/export-multiple', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(formIds)
    });
    
    if (!response.ok) {
      throw new Error(`Error al obtener formularios: ${response.status}`);
    }
    
    const result = await response.json();
    
    // Crear PDF con múltiples páginas
    const pdf = new jsPDF();
    let isFirstPage = true;
    
    for (const formData of result.forms) {
      if (!isFirstPage) {
        pdf.addPage();
      }
      await generateFormPDF(formData, pdf, isFirstPage);
      isFirstPage = false;
    }
    
    // Descargar
    const fileName = `Reporte_${result.count}_formularios_${formatDate(new Date())}.pdf`;
    pdf.save(fileName);
    
    return { success: true, fileName, count: result.count };
    
  } catch (error) {
    console.error('Error exportando PDFs múltiples:', error);
    throw error;
  }
};

/**
 * Genera el contenido PDF de un formulario
 * @param {object} formData - Datos del formulario con template
 * @param {jsPDF} existingPdf - PDF existente (para múltiples formularios)
 * @param {boolean} resetY - Reiniciar posición Y
 * @returns {jsPDF}
 */
const generateFormPDF = async (formData, existingPdf = null, resetY = true) => {
  const pdf = existingPdf || new jsPDF();
  let yPosition = resetY ? 10 : pdf.lastAutoTable?.finalY || 10;
  
  const pageWidth = pdf.internal.pageSize.getWidth();
  const pageHeight = pdf.internal.pageSize.getHeight();
  const margin = 10;
  const usableWidth = pageWidth - (margin * 2);
  
  // ===== ENCABEZADO DEL DOCUMENTO =====
  yPosition = addHeader(pdf, formData, yPosition, margin, usableWidth);
  
  // ===== SECCIÓN HEADER (Información General) =====
  if (formData.template.structure.headerFields && formData.template.structure.headerFields.length > 0) {
    yPosition = addHeaderSection(pdf, formData, yPosition, margin, usableWidth, pageHeight);
  }
  
  // ===== SECCIÓN BODY (Tablas/Elementos) =====
  if (formData.template.structure.bodyElements && formData.template.structure.bodyElements.length > 0) {
    yPosition = addBodySections(pdf, formData, yPosition, margin, usableWidth, pageHeight);
  }
  
  // ===== OBSERVACIONES =====
  if (formData.observaciones) {
    yPosition = addObservations(pdf, formData.observaciones, yPosition, margin, usableWidth, pageHeight);
  }
  
  // ===== FIRMAS =====
  if (formData.template.structure.firmas && formData.template.structure.firmas.length > 0) {
    yPosition = addSignatures(pdf, formData, yPosition, margin, usableWidth, pageHeight);
  }
  
  // ===== PIE DE PÁGINA =====
  addFooter(pdf, formData);
  
  return pdf;
};

/**
 * Agrega el encabezado del documento
 */
const addHeader = (pdf, formData, yPosition, margin, usableWidth) => {
  const template = formData.template;
  
  // Logo o nombre de la empresa (opcional)
  pdf.setFontSize(10);
  pdf.setTextColor(100, 100, 100);
  pdf.text('Frigolab "San Mateo"', margin, yPosition);
  yPosition += 5;
  
  // Título del formulario
  pdf.setFontSize(16);
  pdf.setTextColor(0, 0, 0);
  pdf.setFont(undefined, 'bold');
  const title = template.nombre || 'Formulario';
  pdf.text(title, margin, yPosition);
  yPosition += 8;
  
  // Información del formulario
  pdf.setFontSize(10);
  pdf.setFont(undefined, 'normal');
  
  const infoLeft = [
    `Código: ${template.codigo || 'N/A'}`,
    `Versión: ${template.version || '1.0'}`
  ];
  
  const infoRight = [
    `Fecha: ${formatDate(formData.createdAt)}`,
    `ID: #${formData.formID}`
  ];
  
  infoLeft.forEach((text, i) => {
    pdf.text(text, margin, yPosition + (i * 5));
  });
  
  infoRight.forEach((text, i) => {
    pdf.text(text, pageWidth - margin - pdf.getTextWidth(text), yPosition + (i * 5));
  });
  
  yPosition += 12;
  
  // Línea separadora
  pdf.setLineWidth(0.5);
  pdf.setDrawColor(200, 200, 200);
  pdf.line(margin, yPosition, pageWidth - margin, yPosition);
  yPosition += 8;
  
  return yPosition;
};

/**
 * Agrega la sección de información general (Header Fields)
 */
const addHeaderSection = (pdf, formData, yPosition, margin, usableWidth, pageHeight) => {
  const headerFields = formData.template.structure.headerFields;
  const headerData = formData.data.header || {};
  
  // Título de sección
  pdf.setFontSize(12);
  pdf.setFont(undefined, 'bold');
  pdf.setFillColor(240, 240, 240);
  pdf.rect(margin, yPosition, usableWidth, 7, 'F');
  pdf.text('INFORMACIÓN GENERAL', margin + 2, yPosition + 5);
  yPosition += 10;
  
  // Campos en grid (2 columnas)
  pdf.setFontSize(10);
  pdf.setFont(undefined, 'normal');
  
  const colWidth = usableWidth / 2;
  let col = 0;
  
  headerFields.forEach((field, index) => {
    const value = headerData[field.label] || '';
    const xPosition = margin + (col * colWidth);
    
    // Label
    pdf.setFont(undefined, 'bold');
    pdf.text(`${field.label}:`, xPosition, yPosition);
    
    // Value
    pdf.setFont(undefined, 'normal');
    const labelWidth = pdf.getTextWidth(`${field.label}: `);
    pdf.text(String(value), xPosition + labelWidth, yPosition);
    
    col++;
    if (col >= 2) {
      col = 0;
      yPosition += 6;
    }
  });
  
  if (col > 0) yPosition += 6;
  yPosition += 5;
  
  // Check si necesita nueva página
  if (yPosition > pageHeight - 40) {
    pdf.addPage();
    yPosition = 10;
  }
  
  return yPosition;
};

/**
 * Agrega las secciones del cuerpo (tablas/elementos)
 */
const addBodySections = (pdf, formData, yPosition, margin, usableWidth, pageHeight) => {
  const bodyElements = formData.template.structure.bodyElements;
  const bodyData = formData.data.body || [];
  
  bodyElements.forEach((element, index) => {
    // Check si necesita nueva página
    if (yPosition > pageHeight - 60) {
      pdf.addPage();
      yPosition = 10;
    }
    
    if (element.type === 'table') {
      yPosition = addTableElement(pdf, element, bodyData[index], yPosition, margin, usableWidth);
    } else if (element.type === 'text') {
      yPosition = addTextElement(pdf, element, bodyData[index], yPosition, margin, usableWidth);
    }
    
    yPosition += 5; // Espacio entre secciones
  });
  
  return yPosition;
};

/**
 * Agrega un elemento de tipo tabla
 */
const addTableElement = (pdf, element, elementData, yPosition, margin, usableWidth) => {
  // Título de la tabla
  pdf.setFontSize(11);
  pdf.setFont(undefined, 'bold');
  pdf.setFillColor(230, 230, 230);
  pdf.rect(margin, yPosition, usableWidth, 6, 'F');
  pdf.text(element.title || 'Tabla', margin + 2, yPosition + 4);
  yPosition += 8;
  
  // Preparar datos de la tabla
  const columns = element.columns || [];
  const rows = elementData?.rows || [];
  
  const headers = columns.map(col => col.name || col.label || '');
  const tableData = rows.map(row => 
    columns.map(col => {
      const colName = col.name || col.label || col.id;
      return String(row[colName] || '');
    })
  );
  
  // Renderizar tabla con autoTable
  pdf.autoTable({
    startY: yPosition,
    head: [headers],
    body: tableData,
    theme: 'grid',
    styles: {
      fontSize: 8,
      cellPadding: 2
    },
    headStyles: {
      fillColor: [66, 139, 202],
      textColor: 255,
      fontStyle: 'bold',
      halign: 'center'
    },
    columnStyles: columns.reduce((acc, col, idx) => {
      acc[idx] = {
        cellWidth: col.width ? (usableWidth * col.width / 100) : 'auto',
        halign: col.type === 'number' ? 'right' : 'left'
      };
      return acc;
    }, {}),
    margin: { left: margin, right: margin }
  });
  
  return pdf.lastAutoTable.finalY + 2;
};

/**
 * Agrega un elemento de tipo texto
 */
const addTextElement = (pdf, element, elementData, yPosition, margin, usableWidth) => {
  pdf.setFontSize(10);
  pdf.setFont(undefined, 'bold');
  pdf.text(element.label || element.title || 'Texto:', margin, yPosition);
  yPosition += 5;
  
  pdf.setFont(undefined, 'normal');
  const value = elementData?.value || '';
  const lines = pdf.splitTextToSize(String(value), usableWidth);
  
  lines.forEach(line => {
    pdf.text(line, margin + 5, yPosition);
    yPosition += 5;
  });
  
  return yPosition;
};

/**
 * Agrega observaciones
 */
const addObservations = (pdf, observaciones, yPosition, margin, usableWidth, pageHeight) => {
  // Check si necesita nueva página
  if (yPosition > pageHeight - 40) {
    pdf.addPage();
    yPosition = 10;
  }
  
  // Título
  pdf.setFontSize(11);
  pdf.setFont(undefined, 'bold');
  pdf.setFillColor(230, 230, 230);
  pdf.rect(margin, yPosition, usableWidth, 6, 'F');
  pdf.text('OBSERVACIONES', margin + 2, yPosition + 4);
  yPosition += 10;
  
  // Contenido
  pdf.setFontSize(10);
  pdf.setFont(undefined, 'normal');
  const lines = pdf.splitTextToSize(observaciones, usableWidth - 10);
  
  lines.forEach(line => {
    if (yPosition > pageHeight - 20) {
      pdf.addPage();
      yPosition = 10;
    }
    pdf.text(line, margin + 5, yPosition);
    yPosition += 5;
  });
  
  yPosition += 8;
  
  return yPosition;
};

/**
 * Agrega sección de firmas
 */
const addSignatures = (pdf, formData, yPosition, margin, usableWidth, pageHeight) => {
  const firmas = formData.template.structure.firmas;
  const firmasData = formData.data.firmas || {};
  
  // Check si necesita nueva página
  if (yPosition > pageHeight - 50) {
    pdf.addPage();
    yPosition = 10;
  }
  
  // Título
  pdf.setFontSize(11);
  pdf.setFont(undefined, 'bold');
  pdf.setFillColor(230, 230, 230);
  pdf.rect(margin, yPosition, usableWidth, 6, 'F');
  pdf.text('FIRMAS Y APROBACIONES', margin + 2, yPosition + 4);
  yPosition += 12;
  
  // Firmas en grid
  const signaturesPerRow = 2;
  const signatureWidth = usableWidth / signaturesPerRow;
  let col = 0;
  
  firmas.forEach((firma, index) => {
    const xPosition = margin + (col * signatureWidth);
    const firmante = firmasData[firma.puesto] || '';
    
    // Línea de firma
    pdf.setLineWidth(0.3);
    pdf.line(xPosition + 10, yPosition + 15, xPosition + signatureWidth - 20, yPosition + 15);
    
    // Puesto
    pdf.setFontSize(8);
    pdf.setFont(undefined, 'bold');
    const puestoText = firma.puesto || `Firma ${index + 1}`;
    const puestoWidth = pdf.getTextWidth(puestoText);
    pdf.text(puestoText, xPosition + (signatureWidth / 2) - (puestoWidth / 2), yPosition + 20);
    
    // Nombre del firmante (si existe)
    if (firmante) {
      pdf.setFontSize(9);
      pdf.setFont(undefined, 'normal');
      const firmanteWidth = pdf.getTextWidth(firmante);
      pdf.text(firmante, xPosition + (signatureWidth / 2) - (firmanteWidth / 2), yPosition + 10);
    }
    
    col++;
    if (col >= signaturesPerRow) {
      col = 0;
      yPosition += 25;
    }
  });
  
  if (col > 0) yPosition += 25;
  
  return yPosition;
};

/**
 * Agrega pie de página
 */
const addFooter = (pdf, formData) => {
  const pageCount = pdf.internal.getNumberOfPages();
  const pageHeight = pdf.internal.pageSize.getHeight();
  const pageWidth = pdf.internal.pageSize.getWidth();
  
  for (let i = 1; i <= pageCount; i++) {
    pdf.setPage(i);
    
    pdf.setFontSize(8);
    pdf.setTextColor(150, 150, 150);
    pdf.setFont(undefined, 'normal');
    
    // Texto izquierdo
    pdf.text(`Generado: ${formatDate(new Date())}`, 10, pageHeight - 10);
    
    // Texto derecho
    const pageText = `Página ${i} de ${pageCount}`;
    const pageTextWidth = pdf.getTextWidth(pageText);
    pdf.text(pageText, pageWidth - 10 - pageTextWidth, pageHeight - 10);
    
    // Marca de agua si es histórico
    if (formData.isHistorical) {
      pdf.setFontSize(40);
      pdf.setTextColor(200, 200, 200);
      pdf.setFont(undefined, 'bold');
      pdf.text('HISTÓRICO', pageWidth / 2 - 40, pageHeight / 2, {
        angle: 45,
        opacity: 0.3
      });
    }
  }
};

/**
 * Formatea fecha a string legible
 */
const formatDate = (date) => {
  if (!date) return '';
  const d = new Date(date);
  const day = String(d.getDate()).padStart(2, '0');
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const year = d.getFullYear();
  return `${day}/${month}/${year}`;
};

/**
 * Exporta formularios de un template específico
 */
export const exportTemplateFormsToPDF = async (templateId) => {
  try {
    const response = await fetch(`/api/FilledForms/export-by-template/${templateId}`);
    
    if (!response.ok) {
      throw new Error(`Error al obtener formularios: ${response.status}`);
    }
    
    const result = await response.json();
    
    if (result.count === 0) {
      throw new Error('No hay formularios para exportar');
    }
    
    // Generar PDF con todos los formularios
    return await exportMultipleFormsToPDF(result.forms.map(f => f.formID));
    
  } catch (error) {
    console.error('Error exportando formularios del template:', error);
    throw error;
  }
};

/**
 * Exporta formularios por rango de fechas
 */
export const exportFormsByDateRangeToPDF = async (startDate, endDate) => {
  try {
    const response = await fetch(
      `/api/FilledForms/export-by-date-range?startDate=${startDate}&endDate=${endDate}`
    );
    
    if (!response.ok) {
      throw new Error(`Error al obtener formularios: ${response.status}`);
    }
    
    const result = await response.json();
    
    if (result.count === 0) {
      throw new Error('No hay formularios en el rango de fechas especificado');
    }
    
    return await exportMultipleFormsToPDF(result.forms.map(f => f.formID));
    
  } catch (error) {
    console.error('Error exportando formularios por fecha:', error);
    throw error;
  }
};

export default {
  exportFormToPDF,
  exportMultipleFormsToPDF,
  exportTemplateFormsToPDF,
  exportFormsByDateRangeToPDF
};
